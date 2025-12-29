using Fusion;
using Fusion.Sockets;
using UnityEngine;

public struct NetworkInputData : INetworkInput
{
    public Vector2 movementInput;
    public Vector2 aimDirection;
    public NetworkBool fire;
    public NetworkBool pickup;
    public NetworkBool dash;
}

public class PlayerController : NetworkBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 8f;
    [SerializeField] private float acceleration = 50f;
    [SerializeField] private float friction = 30f;

    [Header("Rotation")]
    [SerializeField] private bool rotateTowardsMovement = false;
    [SerializeField] private bool rotateTowardsAim = true;
    [SerializeField] private float rotationSpeed = 15f;

    [Header("Dash (Optional)")]
    [SerializeField] private bool enableDash = false;
    [SerializeField] private float dashSpeed = 20f;
    [SerializeField] private float dashDuration = 0.2f;
    [SerializeField] private float dashCooldown = 1f;

    [Header("Collision")]
    [SerializeField] private LayerMask obstacleLayer;
    [SerializeField] private float collisionRadius = 0.5f;

    private NetworkTransform networkTransform;
    private PlayerData playerData;

    // Networked state
    [Networked] private Vector2 Velocity { get; set; }
    [Networked] private TickTimer DashTimer { get; set; }
    [Networked] private TickTimer DashCooldownTimer { get; set; }
    [Networked] private Vector2 DashDirection { get; set; }
    [Networked] private NetworkBool IsDashing { get; set; }

    private void Awake()
    {
        networkTransform = GetComponent<NetworkTransform>();
        playerData = GetComponent<PlayerData>();
    }

    public override void FixedUpdateNetwork()
    {
        // Only simulate on state authority (server/host)
        if (!Object.HasStateAuthority)
            return;

        // Don't allow movement if dead
        if (playerData != null && playerData.Dead)
            return;

        if (GetInput(out NetworkInputData input))
        {
            Vector2 newVelocity = Velocity;

            // Check if dashing
            bool isDashing = !DashTimer.ExpiredOrNotRunning(Runner);
            IsDashing = isDashing;

            if (isDashing)
            {
                // During dash, move in dash direction at dash speed
                newVelocity = DashDirection * dashSpeed;
            }
            else
            {
                // Start dash
                if (enableDash && input.dash && DashCooldownTimer.ExpiredOrNotRunning(Runner) && input.movementInput.magnitude > 0.1f)
                {
                    DashDirection = input.movementInput.normalized;
                    DashTimer = TickTimer.CreateFromSeconds(Runner, dashDuration);
                    DashCooldownTimer = TickTimer.CreateFromSeconds(Runner, dashCooldown);
                    newVelocity = DashDirection * dashSpeed;
                }
                else
                {
                    // Normal movement
                    Vector2 targetVelocity = input.movementInput * moveSpeed;

                    if (input.movementInput.magnitude > 0.01f)
                    {
                        // Accelerate towards target velocity
                        newVelocity = Vector2.MoveTowards(newVelocity, targetVelocity, acceleration * Runner.DeltaTime);
                    }
                    else
                    {
                        // Apply friction when no input
                        newVelocity = Vector2.MoveTowards(newVelocity, Vector2.zero, friction * Runner.DeltaTime);
                    }

                    // Clamp to max speed
                    if (newVelocity.magnitude > moveSpeed)
                    {
                        newVelocity = newVelocity.normalized * moveSpeed;
                    }
                }
            }

            Velocity = newVelocity;

            // Move the transform
            Vector3 movement = new Vector3(Velocity.x, Velocity.y, 0) * Runner.DeltaTime;
            Vector3 newPosition = transform.position + movement;

            // Simple collision check (optional)
            Collider2D hit = Physics2D.OverlapCircle(newPosition, collisionRadius, obstacleLayer);
            if (hit == null)
            {
                transform.position = newPosition;
            }
            else
            {
                // Stop velocity if we hit something
                Velocity = Vector2.zero;
            }

            // Handle rotation
            HandleRotation(input);
        }
    }

    private void HandleRotation(NetworkInputData input)
    {
        Quaternion targetRotation = transform.rotation;

        if (rotateTowardsAim && input.aimDirection.magnitude > 0.1f)
        {
            // Rotate towards aim direction
            float targetAngle = Mathf.Atan2(input.aimDirection.y, input.aimDirection.x) * Mathf.Rad2Deg - 90f;
            targetRotation = Quaternion.Euler(0, 0, targetAngle);
        }
        else if (rotateTowardsMovement && input.movementInput.magnitude > 0.1f)
        {
            // Rotate towards movement direction
            float targetAngle = Mathf.Atan2(input.movementInput.y, input.movementInput.x) * Mathf.Rad2Deg - 90f;
            targetRotation = Quaternion.Euler(0, 0, targetAngle);
        }

        // Smoothly interpolate to target rotation
        transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, rotationSpeed * Runner.DeltaTime);
    }

    private void OnDrawGizmosSelected()
    {
        // Draw collision radius
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, collisionRadius);

        // Draw velocity vector
        if (Application.isPlaying)
        {
            Gizmos.color = IsDashing ? Color.yellow : Color.green;
            Gizmos.DrawLine(transform.position, transform.position + new Vector3(Velocity.x, Velocity.y, 0));
        }

        // Draw dash direction when dashing
        if (Application.isPlaying && IsDashing)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, transform.position + new Vector3(DashDirection.x, DashDirection.y, 0) * 2f);
        }
    }
}