using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NewBehaviourScript : MonoBehaviour
{
    [Tooltip("Movement speed in units/second")]
    public float speed = 5f;

    Rigidbody rb;
    Vector3 velocityInput = Vector3.zero;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        // Read WASD keys explicitly
        float x = 0f;
        float z = 0f;
        if (Input.GetKey(KeyCode.A)) x = -1f;
        if (Input.GetKey(KeyCode.D)) x = 1f;
        if (Input.GetKey(KeyCode.W)) z = 1f;
        if (Input.GetKey(KeyCode.S)) z = -1f;

        Vector3 input = new Vector3(x, 0f, z);
        if (input.sqrMagnitude > 1f) input.Normalize();

        velocityInput = input * speed;

        // If no Rigidbody, move directly here (frame-rate independent)
        if (rb == null)
        {
            transform.Translate(velocityInput * Time.deltaTime, Space.World);
        }
    }

    void FixedUpdate()
    {
        // If there's a Rigidbody, move it in FixedUpdate for physics correctness
        if (rb != null)
        {
            rb.MovePosition(rb.position + velocityInput * Time.fixedDeltaTime);
        }
    }
}
