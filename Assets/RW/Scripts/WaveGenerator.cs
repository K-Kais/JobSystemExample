/*
 * Copyright (c) 2020 Razeware LLC
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 *
 * Notwithstanding the foregoing, you may not use, copy, modify, merge, publish, 
 * distribute, sublicense, create a derivative work, and/or sell copies of the 
 * Software in any work that is designed, intended, or marketed for pedagogical or 
 * instructional purposes related to programming, coding, application development, 
 * or information technology.  Permission for such use, copying, modification,
 * merger, publication, distribution, sublicensing, creation of derivative works, 
 * or sale is expressly withheld.
 *    
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 */

using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public class WaveGenerator : MonoBehaviour
{
    [Header("Wave Parameters")]
    public float waveScale;
    public float waveOffsetSpeed;
    public float waveHeight;

    [Header("References and Prefabs")]
    public MeshFilter waterMeshFilter;
    private Mesh waterMesh;

    NativeArray<Vector3> waterVerticies;
    NativeArray<Vector3> waterNormals;

    JobHandle meshModificationJobHandle;
    UpdateMeshJob meshModificationJob;

    private void Start()
    {
        waterMesh = waterMeshFilter.mesh;
        waterMesh.MarkDynamic();
        waterVerticies = new NativeArray<Vector3>(waterMesh.vertices, Allocator.Persistent);
        waterNormals = new NativeArray<Vector3>(waterMesh.normals, Allocator.Persistent);
    }

    private void Update()
    {
        meshModificationJob = new UpdateMeshJob
        {
            verticies = waterVerticies,
            normals = waterNormals,
            offsetSpeed = waveOffsetSpeed,
            height = waveHeight,
            scale = waveScale,
            time = Time.time
        };

        meshModificationJobHandle = meshModificationJob.Schedule(waterVerticies.Length, 64);
    }

    private void LateUpdate()
    {
        meshModificationJobHandle.Complete();

        waterMesh.SetVertices(meshModificationJob.verticies);
        waterMesh.RecalculateNormals();
    }

    private void OnDestroy()
    {
        waterVerticies.Dispose();
        waterNormals.Dispose();
    }
    [BurstCompile]
    private struct UpdateMeshJob : IJobParallelFor
    {
        public NativeArray<Vector3> verticies;
        [ReadOnly] public NativeArray<Vector3> normals;
        public float offsetSpeed;
        public float height;
        public float scale;
        public float time;

        public void Execute(int index)
        {
            if (normals[index].z > 0f)
            {
                var vertex = verticies[index];

                float noiseValue =
                Noise(vertex.x * scale + offsetSpeed * time, vertex.y * scale +
                offsetSpeed * time);

                verticies[index] =
                new Vector3(vertex.x, vertex.y, noiseValue * height + 0.3f);
            }
        }
        private float Noise(float x, float y)
        {
            float2 pos = new float2(x, y);
            return noise.snoise(pos);
        }
    }
}