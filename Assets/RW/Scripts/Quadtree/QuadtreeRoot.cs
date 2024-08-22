using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

public class QuadtreeRoot<TItem, TNode> : IQuadtreeRoot<TItem, TNode>
    where TItem : IItem<TItem, TNode>
    where TNode : INode<TItem, TNode>, new()
{
    public TNode CurrentRootNode { get; }
    public bool Initialized { get; }

    public float MinimumPossibleNodeSize => 1f;

    public QuadtreeRoot(Vector3 center, Vector3 size)
    {
        CurrentRootNode = new TNode
        {
            TreeRoot = this,
            ParentNode = default,
            Bounds = new Bounds(center, size),
        };
        Initialized = true;
    }

    public void Clear()
    {
        throw new System.NotImplementedException();
    }

    public void Expand()
    {
        Debug.Log("Expanding");
    }

    public List<TItem> Find(Bounds bounds)
    {
        IList<TItem> items = new List<TItem>();
        CurrentRootNode.FindAndAddItems(bounds, ref items);

        return (List<TItem>)items;
    }
    public NativeQueue<float3> FindBounds(Bounds bounds)
    {
        NativeQueue<float3> items = new NativeQueue<float3>(Allocator.TempJob);
        CurrentRootNode.FindAndAddItems(bounds, ref items);
        return items;
    }
    //public async Task<List<TItem>> FindAsync(Bounds bounds)
    //{
    //    IList<TItem> items = new List<TItem>();
    //    items = await CurrentRootNode.FindAndAddItemsAsync(bounds, items);

    //    return (List<TItem>)items;
    //}


    public void Insert(TItem item)
    {
        var itemBounds = item.GetBounds();
        while (!CurrentRootNode.Contains(itemBounds)) Expand();
        CurrentRootNode.Insert(item);
    }

    public void Remove(TItem item)
    {
        throw new System.NotImplementedException();
    }
}
