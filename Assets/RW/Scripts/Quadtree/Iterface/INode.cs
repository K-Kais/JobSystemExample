using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

public interface INode<TItem, TNode>
    where TItem : IItem<TItem, TNode>
    where TNode : INode<TItem, TNode>
{
    Bounds Bounds { get; set; }
    TNode ParentNode { get; set; }
    IQuadtreeRoot<TItem, TNode> TreeRoot { get; set; }
    IList<TNode> SubNodes { get; }
    bool Contains(Bounds bounds);
    bool IsEmpty();
    void Insert(TItem item);
    IntraLocation Location(Bounds bounds);
    void FindAndAddItems(Bounds bounds, ref IList<TItem> items);
    void FindAndAddItems(Bounds bounds, ref NativeQueue<float3> items);
    Task<IList<TItem>> FindAndAddItemsAsync(Bounds bounds, IList<TItem> items);
    void AddItems(ref IList<TItem> items, Bounds? bounds = null);
    void AddItems(ref NativeQueue<float3> items, Bounds? bounds = null);
    void Update(TItem item, bool forceInsertionEvaluation = true, bool hasOriginallyContainedItem = true);
    void DrawBounds();
}
