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
    private TransformAccessArray transformAccessArray;
    //private NativeArray<BoidData> boidData;
    private NativeQuadTree<float2> quadTree;


    private void Start()
    {
        QuadBounds bounds = new QuadBounds(float2.zero, new float2(100, 100));
        quadTree = new NativeQuadTree<float2>(bounds, Allocator.Persistent, maxDepth: 6, maxLeafElements: 16);
        transformAccessArray = new TransformAccessArray(boidCount, JobsUtility.JobWorkerCount);
        //boidData = new NativeArray<BoidData>(boidCount, Allocator.TempJob);
        for (int i = 0; i < boidCount; i++)
        {
            float distanceX = UnityEngine.Random.Range(-spawnBounds.x / 2, spawnBounds.x / 2);
            float distanceY = UnityEngine.Random.Range(-spawnBounds.y / 2, spawnBounds.y / 2);
            float direction = UnityEngine.Random.Range(0f, 360f);
            Vector3 spawnPoint = transform.position + new Vector3(distanceX, distanceY, 0);
            Transform boid = Instantiate(boidPrefab, spawnPoint,
                 Quaternion.Euler(Vector3.forward * direction) * boidPrefab.rotation);
            transformAccessArray.Add(boid);
            // boidData[i] = new BoidData(new float2(boid.forward.x, boid.forward.y));
        }
    }
    private void Update()
    {
        var velocities = new NativeArray<QuadElement<float2>>(boidCount, Allocator.TempJob);
        var updateQuadElementJob = new UpdateQuadElementJob
        {
            velocities = velocities
        };
        var updateQuadElementJobHandle = updateQuadElementJob.Schedule(transformAccessArray);
        updateQuadElementJobHandle.Complete();
        quadTree.ClearAndBulkInsert(velocities);

        //var boidInRange = new BoidInRange
        //{
        //    boidData = boidData,
        //    radius = radius,
        //    quadTree = quadTree,
        //    velocities = velocities
        //};
        var boidJob = new BoidJob
        {
            quadTree = quadTree,
            velocities = velocities,
            forwardSpeed = forwardSpeed,
            bounds = spawnBounds,
            center = transform.position,
            turnSpeed = turnSpeed,
            deltaTime = Time.deltaTime,
            radius = radius
        };
        //var boidInRangeHandle = boidInRange.Schedule(transformAccessArray);
        var boidJobHandle = boidJob.Schedule(transformAccessArray);
        //  boidInRangeHandle.Complete();
        boidJobHandle.Complete();
        velocities.Dispose();
    }
    [BurstCompile]
    private struct UpdateQuadElementJob : IJobParallelForTransform
    {
        public NativeArray<QuadElement<float2>> velocities;
        public void Execute(int index, TransformAccess transform)
        {
            float3 position = transform.position;
            float3 forward = transform.localToWorldMatrix.MultiplyVector(Vector3.forward);
            velocities[index] = new QuadElement<float2>
            {
                pos = position.xy,
                element = forward.xy
            };
        }
    }
    [BurstCompile]
    private struct BoidInRange : IJobParallelForTransform
    {
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<BoidData> boidData;
        public NativeQuadTree<float2> quadTree;
        public NativeArray<QuadElement<float2>> velocities;
        public float radius;
        public void Execute(int index, TransformAccess transform)
        {
            var results = new NativeList<QuadElement<float2>>(Allocator.Temp);
            QuadBounds queryBounds = new QuadBounds(new float2(transform.position.x, transform.position.y),
                new float2(radius, radius));
            quadTree.RangeQuery(queryBounds, results);

            foreach (var result in results)
            {
                boidData[index].inRangePositions.Add(result.pos);
                boidData[index].inRangeVelocities.Add(result.element);
            }

            //for (int i = 0; i < boidData.Length; i++)
            //{
            //    if (math.distance(transform.position, boidData[i].position) <= radius)
            //    {
            //        boidData[index].inRangePositions.Add(boidData[i].position);
            //        boidData[index].inRangeVelocities.Add(boidData[i].velocity);
            //    }
            //}
        }
    }
    [BurstCompile]
    private struct BoidJob : IJobParallelForTransform
    {
        public NativeQuadTree<float2> quadTree;
        public NativeArray<QuadElement<float2>> velocities;
        public float3 bounds;
        public float3 center;
        public float forwardSpeed;
        public float turnSpeed;
        public float deltaTime;
        public float radius;
        public void Execute(int index, TransformAccess transform)
        {
            var currentPosition = transform.position;
            Vector2 forward = transform.localToWorldMatrix.MultiplyVector(Vector3.forward);
            var separation = Vector2.zero;
            var alignment = Vector2.zero;
            var cohesion = Vector2.zero;

            var results = new NativeList<QuadElement<float2>>(Allocator.Temp);
            QuadBounds queryBounds = new QuadBounds(new float2(transform.position.x, transform.position.y),
                new float2(radius, radius));
            quadTree.RangeQuery(queryBounds, results);
            var boidcount = results.Length;

            for (int i = 0; i < boidcount; i++)
            {
                separation -= (Vector2)Separation(currentPosition, results[i].pos);
                alignment += (Vector2)results[i].element;
                cohesion += (Vector2)results[i].pos;
            }
            separation = separation.normalized;
            alignment = Aligment(alignment, forward, boidcount);
            cohesion = Cohesion(cohesion, currentPosition, boidcount);
            var direction = (forward + separation + 0.2f * alignment + cohesion).normalized * forwardSpeed;

            Vector2 velocity = velocities[index].element;
            velocity = Vector2.Lerp(velocity, direction, turnSpeed / 2 * deltaTime);
            transform.position += (Vector3)velocity * deltaTime;
            if (!velocity.Equals(float2.zero))
            {
                transform.rotation = Quaternion.Slerp(transform.rotation,
                  Quaternion.LookRotation(velocity), turnSpeed * deltaTime);
            }

            transform = Boundary(transform, currentPosition);
            results.Dispose();
        }
        private float2 Separation(Vector2 currentPosition, Vector2 boidPosition)
        {
            float ratio = Mathf.Clamp01((boidPosition - currentPosition).magnitude / radius);
            return (1 - ratio) * (boidPosition - currentPosition);
        }
        private float2 Aligment(Vector2 direction, Vector2 forward, int boidCount)
        {
            if (boidCount != 0) direction /= boidCount;
            else direction = forward;
            return direction.normalized;
        }
        private float2 Cohesion(Vector2 center, Vector2 currentPosition, int boidCount)
        {
            if (boidCount != 0) center /= boidCount;
            else center = currentPosition;
            return (center - currentPosition).normalized;
        }
        private readonly TransformAccess Boundary(TransformAccess transform, Vector3 currentPosition)
        {
            if (currentPosition.x > center.x + bounds.x / 2 ||
                currentPosition.x < center.x - bounds.x / 2)
            {
                currentPosition.x = currentPosition.x > 0 ?
                center.x - bounds.x / 2 : center.x + bounds.x / 2;
                transform.position = currentPosition;
            }
            if (currentPosition.y > center.y + bounds.y / 2 ||
                currentPosition.y < center.y - bounds.y / 2)
            {
                currentPosition.y = currentPosition.y > 0 ?
                center.y - bounds.y / 2 : center.y + bounds.y / 2;
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
    [BurstCompile]
    private struct BoidData
    {
        public float2 velocity;
        public NativeList<float2> inRangePositions;
        public NativeList<float2> inRangeVelocities;

        public BoidData(float2 velocity)
        {
            this.velocity = velocity;
            inRangePositions = new NativeList<float2>(Allocator.TempJob);
            inRangeVelocities = new NativeList<float2>(Allocator.TempJob);
        }
        public void Dispose()
        {
            inRangePositions.Dispose();
            inRangeVelocities.Dispose();
            inRangePositions = new NativeList<float2>(Allocator.TempJob);
            inRangeVelocities = new NativeList<float2>(Allocator.TempJob);
        }
    }
}