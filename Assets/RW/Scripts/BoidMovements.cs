using Unity.Collections;
using UnityEngine;
using UnityEngine.Jobs;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs.LowLevel.Unsafe;

public class BoidMovements : MonoBehaviour
{
    [SerializeField] private Transform boidPrefab;
    [SerializeField] private GameObjectQuadtreeRoot quadtreeRoot;
    [SerializeField] private int boidCount;
    [SerializeField] private Vector3 spawnBounds;
    [SerializeField] private float radius = 2f;
    [SerializeField] private float forwardSpeed = 5f;
    [SerializeField] private float turnSpeed = 5f;
    private TransformAccessArray transformAccessArray;
    private NativeArray<BoidData> boidData;
    private BoidInRange boidInRange;
    private BoidJob boidJob;
    private JobHandle boidJobHandle;
    private JobHandle boidInRangeHandle;

    private void Start()
    {
        transformAccessArray = new TransformAccessArray(boidCount, JobsUtility.JobWorkerCount);
        boidData = new NativeArray<BoidData>(boidCount, Allocator.Persistent);
        for (int i = 0; i < boidCount; i++)
        {
            float distanceX = UnityEngine.Random.Range(-spawnBounds.x / 2, spawnBounds.x / 2);
            float distanceY = UnityEngine.Random.Range(-spawnBounds.y / 2, spawnBounds.y / 2);
            float direction = UnityEngine.Random.Range(0f, 360f);
            Vector3 spawnPoint = transform.position + new Vector3(distanceX, distanceY, 0);
            Transform boid = Instantiate(boidPrefab, spawnPoint,
                 Quaternion.Euler(Vector3.forward * direction) * boidPrefab.rotation);
            //boid.SetParent(quadtreeRoot);
            boid.GetComponent<Boid>().QuadtreeRootInitialized(quadtreeRoot);
            transformAccessArray.Add(boid);
            boidData[i] = new BoidData
            {
                position = boid.position,
                velocity = new float2(boid.forward.x, boid.forward.y),
                inRangePositions = new NativeList<float3>(Allocator.Persistent),
                inRangeVelocities = new NativeList<float2>(Allocator.Persistent)
            };
        }
    }
    private void Update()
    {
        boidInRange = new BoidInRange
        {
            boidData = boidData,
            radius = radius
        };
        boidJob = new BoidJob
        {
            boidData = boidData,
            forwardSpeed = forwardSpeed,
            bounds = spawnBounds,
            center = transform.position,
            turnSpeed = turnSpeed,
            deltaTime = Time.deltaTime,
            radius = radius
        };
        boidInRangeHandle = boidInRange.Schedule(transformAccessArray);
        boidJobHandle = boidJob.Schedule(transformAccessArray);
    }
    private void LateUpdate()
    {
        boidInRangeHandle.Complete();
        boidJobHandle.Complete();
    }
    [BurstCompile]
    private struct BoidInRange : IJobParallelForTransform
    {
        [NativeDisableContainerSafetyRestriction] public NativeArray<BoidData> boidData;
        public float radius;
        public void Execute(int index, TransformAccess transform)
        {
            for (int i = 0; i < boidData.Length; i++)
            {
                if (math.distance(transform.position, boidData[i].position) <= radius)
                {
                    boidData[index].inRangePositions.Add(boidData[i].position);
                    boidData[index].inRangeVelocities.Add(boidData[i].velocity);
                }
            }
        }
    }
    [BurstCompile]
    private struct BoidJob : IJobParallelForTransform
    {
        [NativeDisableContainerSafetyRestriction] public NativeArray<BoidData> boidData;
        public float3 bounds;
        public float3 center;
        public float forwardSpeed;
        public float turnSpeed;
        public float deltaTime;
        public float radius;
        public void Execute(int index, TransformAccess transform)
        {
            BoidData tempData = boidData[index];
            var boidcount = tempData.inRangePositions.Length;
            var currentPosition = transform.position;
            var forward = transform.localToWorldMatrix.MultiplyVector(Vector3.forward);
            var separation = Vector2.zero;
            var alignment = Vector2.zero;
            var cohesion = Vector2.zero;
            for (int i = 0; i < boidcount; i++)
            {
                separation -= Separation(currentPosition, tempData.inRangePositions[i]);
                alignment += (Vector2)tempData.inRangeVelocities[i];
                cohesion += new Vector2(tempData.inRangePositions[i].x, tempData.inRangePositions[i].y);
            }
            separation = separation.normalized;
            alignment = Aligment(alignment, forward, boidcount);
            cohesion = Cohesion(cohesion, currentPosition, boidcount);
            var direction = ((Vector2)forward
                     + separation
                     + 0.2f * alignment
                     + cohesion
                ).normalized * forwardSpeed;
            Vector2 velocity = tempData.velocity;
            velocity = Vector2.Lerp(velocity, direction, turnSpeed / 2 * deltaTime);
            transform.position += (Vector3)velocity * deltaTime;
            if (!velocity.Equals(float2.zero))
            {
                transform.rotation = Quaternion.Slerp(transform.rotation,
                  Quaternion.LookRotation(velocity), turnSpeed * deltaTime);
            }

            transform = Boundary(transform, currentPosition);
            tempData.position = transform.position;
            tempData.velocity = velocity;
            boidData[index] = tempData;
            ClearList(index);
        }
        private Vector2 Separation(Vector3 currentPosition, Vector3 boidPosition)
        {
            float ratio = Mathf.Clamp01((boidPosition - currentPosition).magnitude / radius);
            return (1 - ratio) * (boidPosition - currentPosition);
        }
        private Vector2 Aligment(Vector3 direction, Vector3 forward, int boidCount)
        {
            if (boidCount != 0) direction /= boidCount;
            else direction = forward;
            return direction.normalized;
        }
        private Vector2 Cohesion(Vector3 center, Vector3 currentPosition, int boidCount)
        {
            if (boidCount != 0) center /= boidCount;
            else center = currentPosition;
            return (center - currentPosition).normalized;
        }
        private void ClearList(int index)
        {
            if (boidData[index].inRangePositions.IsCreated)
            {
                boidData[index].inRangePositions.Clear();
            }

            if (boidData[index].inRangePositions.IsCreated)
            {
                boidData[index].inRangeVelocities.Clear();
            }
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
    public struct BoidData
    {
        public float3 position;
        public float2 velocity;
        public NativeList<float3> inRangePositions;
        public NativeList<float2> inRangeVelocities;
    }
}