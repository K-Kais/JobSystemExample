using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public abstract class GameObjectItemBase<TItem, TNode> : MonoBehaviour, IItem<TItem, TNode>
    where TItem : IItem<TItem, TNode>
    where TNode : INode<TItem, TNode>
{
    public TNode ParentNode { get; set; }
    public IQuadtreeRoot<TItem, TNode> Root { get; private set; }
    protected bool itemInitialized;
    protected bool isOutOfBounds;
    private Bounds lastBounds;
    private Bounds safeBounds;
    private NativeArray<Bounds> boundsArray;
   
    private void Start()
    {
        Debug.Log("Item initialized");
        Init();
    }

    private void LateUpdate()
    {
        UpdateBounds();
    }

    //public void UpdateBounds()
    //{
    //    boundsArray = new NativeArray<Bounds>(1, Allocator.TempJob);
    //    boundsArray[0] = GetBounds();

    //    var updateBoundsJob = new UpdateBoundsJob
    //    {
    //        CurrentBounds = boundsArray,
    //        LastBounds = lastBounds,
    //        SafeBounds = safeBounds,
    //        ForceInsertionEvaluation = false
    //    };

    //    JobHandle jobHandle = updateBoundsJob.Schedule();
    //    jobHandle.Complete();

    //    lastBounds = updateBoundsJob.LastBounds;
    //    safeBounds = updateBoundsJob.SafeBounds;
    //    ParentNode?.Update(This(), updateBoundsJob.ForceInsertionEvaluation);

    //    boundsArray.Dispose();
    //}

    private void UpdateBounds()
    {
        if (GetBounds() != lastBounds)
        {
            var forceInsertionEvaluation = false;
            if (!GetBounds().Intersects(safeBounds) ||
                (GetBounds().size - lastBounds.size).magnitude > 0)
            {
                forceInsertionEvaluation = true;
                safeBounds = GetBounds();
            }
            ParentNode?.Update(This(), forceInsertionEvaluation);
            lastBounds = GetBounds();
        }
    }
    protected virtual void Init()
    {
        itemInitialized = true;
        lastBounds = GetBounds();
        safeBounds = lastBounds;

        if (Root == null)
        {
            if (TryGetComponent(out GameObjectQuadtreeRoot quadtreeRoot) && quadtreeRoot.Initialized)
            {
                Root = (IQuadtreeRoot<TItem, TNode>)quadtreeRoot;
            }
        }

        if (Root != null)
        {
            Root.Insert(This());
        }
    }

    public void QuadtreeRootInitialized(IQuadtreeRoot<TItem, TNode> root)
    {
        Root = root;
        if (itemInitialized) root.Insert(This());
    }
    public abstract Bounds GetBounds();
    protected abstract TItem This();

}

[BurstCompile]
public struct UpdateBoundsJob : IJob
{
    [NativeDisableParallelForRestriction]
    public NativeArray<Bounds> CurrentBounds;
    public Bounds LastBounds;
    public Bounds SafeBounds;
    public bool ForceInsertionEvaluation;

    public void Execute()
    {
        if (CurrentBounds[0] != LastBounds)
        {
            if (!CurrentBounds[0].Intersects(SafeBounds) ||
                (CurrentBounds[0].size - LastBounds.size).magnitude > 0)
            {
                ForceInsertionEvaluation = true;
                SafeBounds = CurrentBounds[0];
            }

            LastBounds = CurrentBounds[0];
        }
    }
}
