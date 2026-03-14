// Unity C# Script - Copy this into your Unity project
// File name: RotateObject.cs
using UnityEngine.UIElements;
using UnityEngine;
using UnityEngine;

public class RotateObject : MonoBehaviour


{
    [Header("Rotation Settings")]
    [Tooltip("Rotation speed in degrees per second")]
    public float rotationSpeed = 90f;

    [Header("Rotation Axis")]
    public bool rotateX = false;
    public bool rotateY = true;
public bool rotateZ = false;

void Update()
{
    // Calculate rotation for this frame
    float rotationThisFrame = rotationSpeed * Time.deltaTime;

    // Create rotation vector based on selected axes
    Vector3 rotation = new Vector3(
        rotateX ? rotationThisFrame : 0f,
        rotateY ? rotationThisFrame : 0f,
        rotateZ ? rotationThisFrame : 0f
    );

    // Apply rotation
    transform.Rotate(rotation);
}

// Optional: Method to change speed at runtime
public void SetRotationSpeed(float newSpeed)
{
    rotationSpeed = newSpeed;
}
}