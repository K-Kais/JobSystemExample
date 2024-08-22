using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Boid : GameObjectItem
{
    [SerializeField] private Renderer renderer;
    public override Bounds GetBounds() => renderer.bounds;
}
