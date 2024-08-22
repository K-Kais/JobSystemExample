using System.ComponentModel;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

public abstract class GameObjectItem : GameObjectItemBase<GameObjectItem, Node<GameObjectItem>>
{
    protected NativeArray<float3> inRangePositions;
    protected NativeArray<float3> inRangeVelocities;

    protected override GameObjectItem This() => this;
}
