using Unity.Collections;
using UnityEngine;
using UnityEngine.Jobs;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Burst;
using NativeQuadTree;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Collections.LowLevel.Unsafe;

public class BoidMovements : MonoBehaviour
{
    [SerializeField] private Transform boidPrefab;
    [SerializeField] private Vector3 spawnBounds;
    [SerializeField] private int boidCount;
    [SerializeField] private float radius = 2f;
    [SerializeField] private float forwardSpeed = 5f;
    [SerializeField] private float turnSpeed = 5f;
    [SerializeField] private float visionAngle = 270f;
    private TransformAccessArray transformAccessArray;
    private NativeQuadTree<float2> quadTree;
    private NativeArray<float2> velocities;


    private void Start()
    {
        QuadBounds bounds = new QuadBounds(float2.zero, new float2(80, 80));
        quadTree = new NativeQuadTree<float2>(bounds, Allocator.Persistent, maxDepth: 8, maxLeafElements: 128);
        transformAccessArray = new TransformAccessArray(boidCount, JobsUtility.JobWorkerCount);
        velocities = new NativeArray<float2>(boidCount, Allocator.Persistent);
        for (int i = 0; i < boidCount; i++)
        {
            float distanceX = UnityEngine.Random.Range(-spawnBounds.x / 2, spawnBounds.x / 2);
            float distanceY = UnityEngine.Random.Range(-spawnBounds.y / 2, spawnBounds.y / 2);
            float direction = UnityEngine.Random.Range(0f, 360f);
            Vector3 spawnPoint = transform.position + new Vector3(distanceX, distanceY, 0);
            Transform boid = Instantiate(boidPrefab, spawnPoint,
                 Quaternion.Euler(Vector3.forward * direction) * boidPrefab.rotation);
            transformAccessArray.Add(boid);
            velocities[i] = new float2(boid.forward.x, boid.forward.y);
        }
    }
    private void Update()
    {
        UpdateQuadTree();
        ApplyRule();
    }
    private void OnDestroy()
    {
        transformAccessArray.Dispose();
        quadTree.Dispose();
        velocities.Dispose();
    }
    private void UpdateQuadTree()
    {
        var forward = new NativeArray<QuadElement<float2>>(boidCount, Allocator.TempJob);
        var updateQuadElementJob = new UpdateQuadElementJob
        {
            foward = forward
        };
        var updateQuadElementJobHandle = updateQuadElementJob.Schedule(transformAccessArray);
        updateQuadElementJobHandle.Complete();
        // quadTree.ClearAndBulkInsert(forward);
        var queryJob = new QuadTreeJobs.AddBulkJob<float2>
        {
            Elements = forward,
            QuadTree = quadTree,
        };
        queryJob.Run();
        forward.Dispose();
    }
    private void ApplyRule()
    {
        var boidMovementsJob = new BoidMovementsJob
        {
            quadTree = quadTree,
            velocities = velocities,
            forwardSpeed = forwardSpeed,
            spawnBounds = spawnBounds,
            center = transform.position,
            turnSpeed = turnSpeed,
            deltaTime = Time.deltaTime,
            radius = radius,
            visionAngle = visionAngle
        };
        var boidMovementsJobHandle = boidMovementsJob.Schedule(transformAccessArray);
        boidMovementsJobHandle.Complete();
    }
    [BurstCompile]
    private struct UpdateQuadElementJob : IJobParallelForTransform
    {
        public NativeArray<QuadElement<float2>> foward;
        public void Execute(int index, TransformAccess transform)
        {
            float3 position = transform.position;
            float3 forward = transform.localToWorldMatrix.MultiplyVector(Vector3.forward);
            foward[index] = new QuadElement<float2>
            {
                pos = position.xy,
                element = forward.xy
            };
        }
    }
    //[BurstCompile]
    //private struct BoidInRange : IJobParallelForTransform
    //{
    //    [NativeDisableContainerSafetyRestriction]
    //    public NativeArray<BoidData> boidData;
    //    public NativeQuadTree<float2> quadTree;
    //    public NativeArray<QuadElement<float2>> velocities;
    //    public float radius;
    //    public void Execute(int index, TransformAccess transform)
    //    {
    //        var results = new NativeList<QuadElement<float2>>(Allocator.Temp);
    //        QuadBounds queryBounds = new QuadBounds(new float2(transform.position.x, transform.position.y),
    //            new float2(radius, radius));
    //        quadTree.RangeQuery(queryBounds, results);

    //        foreach (var result in results)
    //        {
    //            boidData[index].inRangePositions.Add(result.pos);
    //            boidData[index].inRangeVelocities.Add(result.element);
    //        }

    //        //for (int i = 0; i < boidData.Length; i++)
    //        //{
    //        //    if (math.distance(transform.position, boidData[i].position) <= radius)
    //        //    {
    //        //        boidData[index].inRangePositions.Add(boidData[i].position);
    //        //        boidData[index].inRangeVelocities.Add(boidData[i].velocity);
    //        //    }
    //        //}
    //    }
    //}
    [BurstCompile]
    private struct BoidMovementsJob : IJobParallelForTransform
    {
        public NativeQuadTree<float2> quadTree;
        public NativeArray<float2> velocities;
        public float3 spawnBounds;
        public float3 center;
        public float forwardSpeed;
        public float turnSpeed;
        public float deltaTime;
        public float radius;
        public float visionAngle;
        public void Execute(int index, TransformAccess transform)
        {
            float3 currentPosition = transform.position;
            Vector2 currentForward = transform.localToWorldMatrix.MultiplyVector(Vector3.forward);
            var separation = Vector2.zero;
            var alignment = Vector2.zero;
            var cohesion = Vector2.zero;

            var results = new NativeList<QuadElement<float2>>(Allocator.Temp);
            QuadBounds queryBounds = new QuadBounds(currentPosition.xy, new float2(radius, radius));
            quadTree.RangeQuery(queryBounds, results);
            var boidcount = results.Length;
            for (int i = 0; i < boidcount; i++)
            {
                if (!InVisionCone(currentPosition.xy, results[i].pos, currentForward))
                {
                    //Debug.DrawLine(currentPosition, (Vector2)results[i].pos, Color.red);
                    results.RemoveAtSwapBack(i);
                    boidcount--;
                    i--;
                    continue;
                }

                cohesion += (Vector2)results[i].pos;
                separation -= Separation(currentPosition.xy, results[i].pos);
                alignment += (Vector2)results[i].element;
            }
            separation = separation.normalized;
            alignment = Aligment(alignment, currentForward, boidcount);
            cohesion = Cohesion(cohesion, currentPosition.xy, boidcount);
            var direction = (currentForward
                + separation
                + 0.2f * alignment
                + cohesion
                ).normalized * forwardSpeed;

            Vector2 velocity = velocities[index];
            velocity = Vector2.Lerp(velocity, direction, turnSpeed / 2 * deltaTime);
            transform.position += (Vector3)velocity * deltaTime;
            if (velocity != Vector2.zero)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation,
                  Quaternion.LookRotation(velocity), turnSpeed * deltaTime);
            }
            velocities[index] = velocity;
            transform = Boundary(transform, currentPosition);
            results.Dispose();
        }
        private bool InVisionCone(Vector2 currentPostition, Vector2 boidPosition, Vector2 currentForward)
        {
            Vector2 directionToPosition = boidPosition - currentPostition;
            float dotProduct = Vector2.Dot(currentForward.normalized, directionToPosition);
            float cosHalfVisionAngle = Mathf.Cos(visionAngle * 0.5f * Mathf.Deg2Rad);
            return dotProduct >= cosHalfVisionAngle;
        }
        private Vector2 Separation(Vector2 currentPosition, Vector2 boidPosition)
        {
            float ratio = Mathf.Clamp01((boidPosition - currentPosition).magnitude / radius);
            return (1 - ratio) * (boidPosition - currentPosition);
        }
        private Vector2 Aligment(Vector2 direction, Vector2 currentForward, int boidCount)
        {
            if (boidCount != 0) direction /= boidCount;
            else direction = currentForward;
            return direction.normalized;
        }
        private Vector2 Cohesion(Vector2 center, Vector2 currentPosition, int boidCount)
        {
            if (boidCount != 0 && center != Vector2.zero) center /= boidCount;
            else center = currentPosition;
            return (center - currentPosition).normalized;
        }
        private readonly TransformAccess Boundary(TransformAccess transform, float3 currentPosition)
        {
            if (currentPosition.x > center.x + spawnBounds.x / 2 ||
                currentPosition.x < center.x - spawnBounds.x / 2)
            {
                currentPosition.x = currentPosition.x > 0 ?
                center.x - spawnBounds.x / 2 : center.x + spawnBounds.x / 2;
                transform.position = currentPosition;
            }
            if (currentPosition.y > center.y + spawnBounds.y / 2 ||
                currentPosition.y < center.y - spawnBounds.y / 2)
            {
                currentPosition.y = currentPosition.y > 0 ?
                center.y - spawnBounds.y / 2 : center.y + spawnBounds.y / 2;
                transform.position = currentPosition;
            }

            return transform;
        }
    }
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireCube(transform.position, spawnBounds);
    }
}