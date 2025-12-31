using Fusion;
using UnityEngine;

// Define the button bits
public enum MyButtons
{
    Fire = 0,
    Pickup = 1,
    Dash = 2
}

public struct NetworkInputData : INetworkInput
{
    public Vector2 movementInput;
    public Vector2 aimDirection;
    public Vector2 mouseWorldPosition;
    public NetworkButtons buttons; // Handles Fire, Pickup, and Dash reliably
    public int inputTick; // Add this - tick when input was created for deterministic randomness
}