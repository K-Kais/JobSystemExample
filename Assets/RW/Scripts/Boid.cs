using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Boid : MonoBehaviour
{
    private void Update()
    {
        Debug.DrawLine(transform.position, transform.position + transform.forward, Color.red);
    }
}