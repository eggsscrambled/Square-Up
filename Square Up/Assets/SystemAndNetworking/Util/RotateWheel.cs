using UnityEngine;
using Fusion;

public class RotateWheel : NetworkBehaviour
{
    public float rotateSpeed;

    public override void FixedUpdateNetwork()
    {
        if (Object.HasStateAuthority)
        {
            transform.Rotate(0, 0, rotateSpeed * Runner.DeltaTime);
        }
    }
}
