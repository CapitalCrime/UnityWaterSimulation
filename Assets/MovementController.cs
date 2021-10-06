using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MovementController : MonoBehaviour
{
    // Start is called before the first frame update
    Transform child;
    void Start()
    {
        child = transform.GetChild(0);
    }

    float moveSpeed = 25;
    // Update is called once per frame
    void Update()
    {

        if (Input.GetKey(KeyCode.W))
        {
            transform.position += transform.forward * Time.deltaTime * moveSpeed;
        }
        else if (Input.GetKey(KeyCode.S))
        {
            transform.position -= transform.forward * Time.deltaTime * moveSpeed;
        }
        if (Input.GetKey(KeyCode.D))
        {
            transform.position += transform.right * Time.deltaTime * moveSpeed;
        }
        else if (Input.GetKey(KeyCode.A))
        {
            transform.position -= transform.right * Time.deltaTime * moveSpeed;
        }
        if (Input.GetKey(KeyCode.E))
        {
            transform.position += transform.up * Time.deltaTime * moveSpeed;
        }
        else if (Input.GetKey(KeyCode.Q))
        {
            transform.position -= transform.up * Time.deltaTime * moveSpeed;
        }

        if (Input.GetMouseButton(0))
        {
            transform.Rotate(0, -90 * Time.deltaTime, 0);
        }
        else if (Input.GetMouseButton(1))
        {
            transform.Rotate(0, 90 * Time.deltaTime, 0);
        }

        if (Input.mouseScrollDelta.y > 0)
        {
            child.Rotate(3, 0, 0);
        }
        else if (Input.mouseScrollDelta.y < 0)
        {
            child.Rotate(-3, 0, 0);
        }
    }
}
