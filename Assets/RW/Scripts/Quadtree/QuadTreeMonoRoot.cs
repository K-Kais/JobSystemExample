using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

public class QuadTreeMonoRoot<TItem, TNode> : MonoBehaviour, IQuadtreeRoot<TItem, TNode>
    where TItem : IItem<TItem, TNode>
    where TNode : INode<TItem, TNode>, new()
{
    [SerializeField] protected Vector3 defaultRootNodeSize;
    [SerializeField] protected float minimumPossibleNodeSize;

    public TNode CurrentRootNode => treeRoot.CurrentRootNode;

    public bool Initialized => throw new NotImplementedException();

    public float MinimumPossibleNodeSize => minimumPossibleNodeSize;
    protected QuadtreeRoot<TItem, TNode> treeRoot = null;
    protected void Awake()
    {
        Init();
        Debug.Log("Quadtree initialized");
    }
    private void OnDrawGizmosSelected()
    {
        treeRoot?.CurrentRootNode.DrawBounds();
    }
    protected void Init()
    {
        if (treeRoot == null)
        {
            treeRoot = new QuadtreeRoot<TItem, TNode>(transform.position, defaultRootNodeSize);
        }
        else treeRoot.Clear();
        BroadcastMessage(IItem<TItem, TNode>.QuadtreeRootInitializedMethodName, this,
            SendMessageOptions.DontRequireReceiver);
    }

    public void Clear()
    {
        throw new System.NotImplementedException();
    }

    public void Expand()
    {
        throw new System.NotImplementedException();
    }

    public List<TItem> Find(Bounds bounds) => treeRoot.Find(bounds);
    public NativeQueue<float3> FindBounds(Bounds bounds) => treeRoot.FindBounds(bounds);
    // public async Task<List<TItem>> FindAsync(Bounds bounds) => await treeRoot.FindAsync(bounds);

    public void Insert(TItem item) => treeRoot.Insert(item);

    public void Remove(TItem item)
    {
        throw new System.NotImplementedException();
    }
}
