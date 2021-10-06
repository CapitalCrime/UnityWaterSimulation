using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraParts : MonoBehaviour
{
    public Material material;
    protected int number = 200000;
    protected ComputeBuffer compute_buffer;

    struct Point
    {
        public Vector3 position;
    }

    void Start()
    {
        compute_buffer = new ComputeBuffer(number, sizeof(float) * 3, ComputeBufferType.Default);
        Point[] cloud = new Point[number];
        for (uint i = 0; i < number; ++i)
        {
            cloud[i] = new Point();
            cloud[i].position = new Vector3();
            cloud[i].position.x = Random.Range(-7.0f, 7.0f);
            cloud[i].position.y = Random.Range(-2, 10.0f);
            cloud[i].position.z = Random.Range(0, 3);
        }
        compute_buffer.SetData(cloud);
    }

    void OnPostRender()
    {
        material.SetPass(0);
        material.SetBuffer("cloud", compute_buffer);
        Graphics.DrawProceduralNow(MeshTopology.Points, number, 1);
    }

    void OnDestroy()
    {
        //compute_buffer.Release();
    }
}
