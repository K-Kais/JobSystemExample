using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

public interface IQuadtreeRoot<TItem, TNode>
    where TItem : IItem<TItem, TNode>
    where TNode : INode<TItem, TNode>
{
    bool Initialized { get; }
    TNode CurrentRootNode { get; }
    float MinimumPossibleNodeSize { get; }
    void Insert(TItem item);
    void Remove(TItem item);
    void Expand();
    List<TItem> Find(Bounds bounds);
    NativeQueue<float3> FindBounds(Bounds bounds);
    //Task<List<TItem>> FindAsync(Bounds bounds);
    void Clear();
}
