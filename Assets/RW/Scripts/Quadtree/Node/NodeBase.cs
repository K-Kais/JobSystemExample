using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;

public abstract class NodeBase<TItem, TNode> : INode<TItem, TNode>
    where TItem : IItem<TItem, TNode>
    where TNode : NodeBase<TItem, TNode>, new()
{
    public Bounds Bounds { get; set; }
    public TNode ParentNode { get; set; }
    public IQuadtreeRoot<TItem, TNode> TreeRoot { get; set; }
    public IList<TNode> SubNodes { get; }
    private readonly HashSet<TItem> items;
    public NodeBase()
    {
        SubNodes = new List<TNode>(4);
        items = new HashSet<TItem>();
    }

    public bool Contains(Bounds bounds) =>
        bounds.min.x >= Bounds.min.x &&
        bounds.min.y >= Bounds.min.y &&
        bounds.max.x < Bounds.max.x &&
        bounds.max.y < Bounds.max.y;

    public void Insert(TItem item)
    {
        if (SubNodes.Count == 0) CreateSubNodes();
        if (SubNodes.Count == 0)
        {
            items.Add(item);
            item.ParentNode = (TNode)this;
            return;
        }

        var itemBounds = item.GetBounds();
        IntraLocation itemBoundsLocation = Location(itemBounds);
        switch (itemBoundsLocation)
        {
            case IntraLocation.UPPER_RIGHT:
            case IntraLocation.UPPER_LEFT:
            case IntraLocation.LOWER_RIGHT:
            case IntraLocation.LOWER_LEFT:
                SubNodes[(int)itemBoundsLocation].Insert(item);
                break;
            default:
                //Debug.LogError("Item bounds spanning multiple nodes");
                items.Add(item);
                item.ParentNode = (TNode)this;
                break;
        }
    }

    private void CreateSubNodes()
    {
        var subBoundsSize = Bounds.size / 2f;
        if (subBoundsSize.x < TreeRoot.MinimumPossibleNodeSize ||
            subBoundsSize.y < TreeRoot.MinimumPossibleNodeSize) return;

        var centerOffset = subBoundsSize / 2f;

        SubNodes.Insert((int)IntraLocation.UPPER_RIGHT, new TNode()
        {
            TreeRoot = TreeRoot,
            ParentNode = (TNode)this,
            Bounds = new Bounds(Bounds.center + centerOffset, subBoundsSize),
        });
        centerOffset.x *= -1f;
        SubNodes.Insert((int)IntraLocation.UPPER_LEFT, new TNode()
        {
            TreeRoot = TreeRoot,
            ParentNode = (TNode)this,
            Bounds = new Bounds(Bounds.center + centerOffset, subBoundsSize),
        });
        centerOffset.y *= -1f;
        SubNodes.Insert((int)IntraLocation.LOWER_LEFT, new TNode()
        {
            TreeRoot = TreeRoot,
            ParentNode = (TNode)this,
            Bounds = new Bounds(Bounds.center + centerOffset, subBoundsSize),
        });
        centerOffset.x *= -1f;
        SubNodes.Insert((int)IntraLocation.LOWER_RIGHT, new TNode()
        {
            TreeRoot = TreeRoot,
            ParentNode = (TNode)this,
            Bounds = new Bounds(Bounds.center + centerOffset, subBoundsSize),
        });
    }
    public IntraLocation Location(Bounds bounds)
    {
        if (bounds.min.y >= Bounds.center.y)
        {
            return bounds.min.x >= Bounds.center.x ? IntraLocation.UPPER_RIGHT
                 : bounds.max.x < Bounds.center.x ? IntraLocation.UPPER_LEFT : IntraLocation.SPANNING_UPPER;
        }
        else if (bounds.max.y < Bounds.center.y)
        {
            return bounds.min.x >= Bounds.center.x ? IntraLocation.LOWER_RIGHT
                 : bounds.max.x < Bounds.center.x ? IntraLocation.LOWER_LEFT : IntraLocation.SPANNING_LOWER;
        }
        else
        {
            return bounds.min.x >= Bounds.center.x ? IntraLocation.SPANNING_RIGHT
                 : bounds.max.x < Bounds.center.x ? IntraLocation.SPANNING_LEFT : IntraLocation.SPANNING_ALL;
        }
    }

    public void FindAndAddItems(Bounds bounds, ref IList<TItem> items)
    {
        if (SubNodes.Count == 0)
        {
            AddOwnItems(ref items);
            return;
        }

        AddOwnItems(ref items, bounds);
        IntraLocation boundsLocation = Location(bounds);

        switch (boundsLocation)
        {
            case IntraLocation.UPPER_RIGHT:
            case IntraLocation.UPPER_LEFT:
            case IntraLocation.LOWER_RIGHT:
            case IntraLocation.LOWER_LEFT:
                SubNodes[(int)boundsLocation].FindAndAddItems(bounds, ref items);
                break;
            case IntraLocation.SPANNING_LEFT:
                SubNodes[(int)IntraLocation.UPPER_LEFT].AddItems(ref items, bounds);
                SubNodes[(int)IntraLocation.LOWER_LEFT].AddItems(ref items, bounds);
                break;
            case IntraLocation.SPANNING_RIGHT:
                SubNodes[(int)IntraLocation.UPPER_RIGHT].AddItems(ref items, bounds);
                SubNodes[(int)IntraLocation.LOWER_RIGHT].AddItems(ref items, bounds);
                break;
            case IntraLocation.SPANNING_UPPER:
                SubNodes[(int)IntraLocation.UPPER_RIGHT].AddItems(ref items, bounds);
                SubNodes[(int)IntraLocation.UPPER_LEFT].AddItems(ref items, bounds);
                break;
            case IntraLocation.SPANNING_LOWER:
                SubNodes[(int)IntraLocation.LOWER_RIGHT].AddItems(ref items, bounds);
                SubNodes[(int)IntraLocation.LOWER_LEFT].AddItems(ref items, bounds);
                break;

            case IntraLocation.SPANNING_ALL:
            default:
                AddSubNodesItems(ref items, bounds);
                break;
        }
    }
    public void FindAndAddItems(Bounds bounds, ref NativeQueue<float3> items)
    {
        if (SubNodes.Count == 0)
        {
            //AddOwnItems(ref items);
            return;
        }

        AddOwnItems(ref items, bounds);
        IntraLocation boundsLocation = Location(bounds);

        switch (boundsLocation)
        {
            case IntraLocation.UPPER_RIGHT:
            case IntraLocation.UPPER_LEFT:
            case IntraLocation.LOWER_RIGHT:
            case IntraLocation.LOWER_LEFT:
                SubNodes[(int)boundsLocation].FindAndAddItems(bounds, ref items);
                break;
            case IntraLocation.SPANNING_LEFT:
                SubNodes[(int)IntraLocation.UPPER_LEFT].AddItems(ref items, bounds);
                SubNodes[(int)IntraLocation.LOWER_LEFT].AddItems(ref items, bounds);
                break;
            case IntraLocation.SPANNING_RIGHT:
                SubNodes[(int)IntraLocation.UPPER_RIGHT].AddItems(ref items, bounds);
                SubNodes[(int)IntraLocation.LOWER_RIGHT].AddItems(ref items, bounds);
                break;
            case IntraLocation.SPANNING_UPPER:
                SubNodes[(int)IntraLocation.UPPER_RIGHT].AddItems(ref items, bounds);
                SubNodes[(int)IntraLocation.UPPER_LEFT].AddItems(ref items, bounds);
                break;
            case IntraLocation.SPANNING_LOWER:
                SubNodes[(int)IntraLocation.LOWER_RIGHT].AddItems(ref items, bounds);
                SubNodes[(int)IntraLocation.LOWER_LEFT].AddItems(ref items, bounds);
                break;

            case IntraLocation.SPANNING_ALL:
            default:
                AddSubNodesItems(ref items, bounds);
                break;
        }
    }
    public void AddItems(ref NativeQueue<float3> items, Bounds? bounds = null)
    {
        AddOwnItems(ref items, bounds);
        AddSubNodesItems(ref items, bounds);
    }
    public void AddItems(ref IList<TItem> items, Bounds? bounds = null)
    {
        AddOwnItems(ref items, bounds);
        AddSubNodesItems(ref items, bounds);
    }
    private void AddOwnItems(ref IList<TItem> items, Bounds? bounds = null)
    {
        var itemSource = bounds != null
                ? this.items.Where(item => item.GetBounds().Intersects((Bounds)bounds))
                : this.items;
        items.AddRange(itemSource);
    }
    private void AddOwnItems(ref NativeQueue<float3> items, Bounds? bounds = null)
    {
        AddOwnItemsJob job = new AddOwnItemsJob
        {
            Items = new NativeArray<Bounds>(this.items.
            Select(item => item.GetBounds()).ToArray(), Allocator.TempJob),
            Bounds = bounds ?? Bounds,
            Results = items.AsParallelWriter()
        };
        JobHandle jobHandle = job.Schedule(this.items.Count, 32);
        jobHandle.Complete();
        job.Items.Dispose();
    }
    [BurstCompile]
    private struct AddOwnItemsJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<Bounds> Items;
        [ReadOnly] public Bounds Bounds;
        public NativeQueue<float3>.ParallelWriter Results;

        public void Execute(int index)
        {
            var item = Items[index];
            if (item.Intersects(Bounds)) Results.Enqueue((float3)item.center);
        }
    }
    public HashSet<TItemData<TItem, TNode>> ConvertToStruct(HashSet<TItem> item)
    {
        HashSet<TItemData<TItem, TNode>> itemData = new HashSet<TItemData<TItem, TNode>>();
        foreach (var i in item)
        {
            TItemData<TItem, TNode> data = new TItemData<TItem, TNode>();
            data.Bounds = i.GetBounds();
            data.ParentNode = i.ParentNode;
            itemData.Add(data);
        }
        return itemData;
    }
    //public TItem ConvertToClass(TItemData<TItem, TNode> itemData)
    //{
    //    TItem item = new TItem(); 
    //    item.ParentNode = itemData.ParentNode;
    //    return item;
    //}

    //public struct ProcessItemsJob : IJobParallelFor
    //{
    //    [ReadOnly] public NativeArray<TItemData<TItem, TNode>> Items;
    //    public NativeArray<bool> Results; // Kết quả sau khi xử lý

    //    public void Execute(int index)
    //    {
    //        var item = Items[index];
    //        Results[index] = item.Bounds.Intersects(someBounds); // Xử lý logic trong job
    //    }
    //}
    private void AddSubNodesItems(ref IList<TItem> items, Bounds? bounds = null)
    {
        foreach (var subNode in SubNodes) subNode.AddItems(ref items, bounds);
    }
    private void AddSubNodesItems(ref NativeQueue<float3> items, Bounds? bounds = null)
    {
        foreach (var subNode in SubNodes) subNode.AddItems(ref items, bounds);
    }
    public void DrawBounds()
    {
        Gizmos.DrawWireCube(Bounds.center, Bounds.size);
        foreach (var subNode in SubNodes) subNode.DrawBounds();
    }
    public async Task<IList<TItem>> FindAndAddItemsAsync(Bounds bounds, IList<TItem> items)
    {

        Debug.DrawLine(Bounds.min, new Vector3(Bounds.max.x, Bounds.min.y, Bounds.min.z), Color.green, 1.0f);
        Debug.DrawLine(Bounds.min, new Vector3(Bounds.min.x, Bounds.max.y, Bounds.min.z), Color.green, 1.0f);
        Debug.DrawLine(Bounds.max, new Vector3(Bounds.max.x, Bounds.min.y, Bounds.min.z), Color.green, 1.0f);
        Debug.DrawLine(Bounds.max, new Vector3(Bounds.min.x, Bounds.max.y, Bounds.min.z), Color.green, 1.0f);
        await Task.Delay(1000);

        if (SubNodes.Count == 0)
        {
            Debug.Log("No subnodes");
            return items;
        }

        AddOwnItems(ref items, bounds);
        var boundsLocation = Location(bounds);
        switch (boundsLocation)
        {
            case IntraLocation.UPPER_RIGHT:
            case IntraLocation.UPPER_LEFT:
            case IntraLocation.LOWER_RIGHT:
            case IntraLocation.LOWER_LEFT:
                items = await SubNodes[(int)boundsLocation].FindAndAddItemsAsync(bounds, items);
                break;
            case IntraLocation.SPANNING_LEFT:
                items = await SubNodes[(int)IntraLocation.UPPER_LEFT].AddItemsAsync(items, bounds);
                items = await SubNodes[(int)IntraLocation.LOWER_LEFT].AddItemsAsync(items, bounds);
                break;
            case IntraLocation.SPANNING_RIGHT:
                items = await SubNodes[(int)IntraLocation.UPPER_RIGHT].AddItemsAsync(items, bounds);
                items = await SubNodes[(int)IntraLocation.LOWER_RIGHT].AddItemsAsync(items, bounds);
                break;
            case IntraLocation.SPANNING_UPPER:
                items = await SubNodes[(int)IntraLocation.UPPER_RIGHT].AddItemsAsync(items, bounds);
                items = await SubNodes[(int)IntraLocation.UPPER_LEFT].AddItemsAsync(items, bounds);
                break;
            case IntraLocation.SPANNING_LOWER:
                items = await SubNodes[(int)IntraLocation.LOWER_RIGHT].AddItemsAsync(items, bounds);
                items = await SubNodes[(int)IntraLocation.LOWER_LEFT].AddItemsAsync(items, bounds);
                break;

            case IntraLocation.SPANNING_ALL:
            default:
                items = await AddSubNodesItemsAsync(items, bounds);
                break;
        }

        return items;
    }
    public async Task<IList<TItem>> AddItemsAsync(IList<TItem> items, Bounds? bounds = null)
    {
        Debug.DrawLine(Bounds.min, new Vector3(Bounds.max.x, Bounds.min.y, Bounds.min.z), Color.green, 1.0f);
        Debug.DrawLine(Bounds.min, new Vector3(Bounds.min.x, Bounds.max.y, Bounds.min.z), Color.green, 1.0f);
        Debug.DrawLine(Bounds.max, new Vector3(Bounds.max.x, Bounds.min.y, Bounds.min.z), Color.green, 1.0f);
        Debug.DrawLine(Bounds.max, new Vector3(Bounds.min.x, Bounds.max.y, Bounds.min.z), Color.green, 1.0f);
        await Task.Delay(1000);
        AddOwnItems(ref items, bounds);
        items = await AddSubNodesItemsAsync(items, bounds);
        return items;
    }
    public async Task<IList<TItem>> AddSubNodesItemsAsync(IList<TItem> items, Bounds? bounds = null)
    {
        foreach (var subNode in SubNodes) await subNode.AddItemsAsync(items, bounds);
        return items;
    }

    public void Update(TItem item, bool forceInsertionEvaluation = true, bool hasOriginallyContainedItem = true)
    {
        if (Contains(item.GetBounds()))
        {
            if (hasOriginallyContainedItem)
            {
                if (forceInsertionEvaluation)
                {
                    RemoveOwnItem(item);
                    Insert(item);
                }
                return;
            }
            Insert(item);
            return;
        }
        if (ParentNode == null)
        {
            TreeRoot.Expand();
            if (ParentNode == null)
            {
                //Debug.LogError("Tree root expansion failed for item: " + item.ToString());
                return;
            }
        }
        if (hasOriginallyContainedItem) RemoveOwnItem(item);
        ParentNode.Update(item, forceInsertionEvaluation, false);
    }

    private void RemoveOwnItem(TItem item)
    {
        items.Remove(item);
        item.ParentNode = null;
        if (IsEmpty()) SubNodes.Clear();
    }

    public bool IsEmpty()
    {
        if (items.Count > 0) return false;

        foreach (var subNode in SubNodes) if (!subNode.IsEmpty()) return false;

        return true;
    }
}
public enum IntraLocation
{
    UPPER_RIGHT,
    UPPER_LEFT,
    LOWER_LEFT,
    LOWER_RIGHT,
    SPANNING_RIGHT,
    SPANNING_LEFT,
    SPANNING_UPPER,
    SPANNING_LOWER,
    SPANNING_ALL
}
public struct TItemData<TItem, TNode> : IItem<TItem, TNode>
    where TItem : IItem<TItem, TNode>
    where TNode : INode<TItem, TNode>
{
    public Bounds Bounds;

    public TNode ParentNode { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public Bounds GetBounds()
    {
        throw new NotImplementedException();
    }

    public void QuadtreeRootInitialized(IQuadtreeRoot<TItem, TNode> root)
    {
        throw new NotImplementedException();
    }
}