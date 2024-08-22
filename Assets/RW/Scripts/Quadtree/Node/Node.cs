public class Node<TItem> : NodeBase<TItem, Node<TItem>>
    where TItem : IItem<TItem, Node<TItem>>
{
}
