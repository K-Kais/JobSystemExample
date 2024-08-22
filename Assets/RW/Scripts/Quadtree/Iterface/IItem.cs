using UnityEngine;
public interface IItem<TItem, TNode>
    where TItem : IItem<TItem, TNode>
    where TNode : INode<TItem, TNode>
{
    Bounds GetBounds();
    TNode ParentNode { get; set; }
    static string QuadtreeRootInitializedMethodName => nameof(QuadtreeRootInitialized);
    void QuadtreeRootInitialized(IQuadtreeRoot<TItem, TNode> root);
}