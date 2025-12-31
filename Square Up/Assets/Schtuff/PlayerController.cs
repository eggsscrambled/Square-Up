using Fusion;
using Fusion.Sockets;
using UnityEngine;

public struct NetworkInputData : INetworkInput
{
    public Vector2 movementInput;
    public NetworkBool jump;
    public Vector2 aimDirection;
    public NetworkBool fire;
    public NetworkBool pickup; // NEW: for picking up weapons
}

public class PlayerController : NetworkBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 8f;
    [SerializeField] private float jumpForce = 15f;
    [SerializeField] private float airControl = 0.8f;
    [SerializeField] private float acceleration = 50f;
    [SerializeField] private float friction = 30f;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.2f;
    [SerializeField] private LayerMask groundLayer;

    [Header("Wall Check")]
    [SerializeField] private Transform wallCheck;
    [SerializeField] private float wallCheckDistance = 0.5f;
    [SerializeField] private float wallSlideSpeed = 2f;
    [SerializeField] private float wallJumpForce = 15f;
    [SerializeField] private float wallJumpDirection = 1.2f;
    [SerializeField] private float wallJumpTime = 0.2f;

    [Header("Slope Handling")]
    [SerializeField] private float maxSlopeAngle = 45f;
    [SerializeField] private float slopeCheckDistance = 0.6f;
    [SerializeField] private float slopeDownForce = 8f;
    [SerializeField] private bool alignToSlope = true;
    [SerializeField] private float rotationSpeed = 10f;

    [Header("Physics")]
    [SerializeField] private float gravity = -30f;
    [SerializeField] private float fallGravityMultiplier = 2f;
    [SerializeField] private float lowJumpMultiplier = 1.5f;

    private NetworkTransform networkTransform;
    private PlayerData playerData;

    // Networked state
    [Networked] private Vector2 Velocity { get; set; }
    [Networked] private NetworkBool IsGrounded { get; set; }
    [Networked] private NetworkBool OnSlope { get; set; }
    [Networked] private float SlopeAngle { get; set; }
    [Networked] private NetworkBool IsTouchingWall { get; set; }
    [Networked] private NetworkBool IsWallSliding { get; set; }
    [Networked] private int WallDirection { get; set; }
    [Networked] private TickTimer CoyoteTimer { get; set; }
    [Networked] private TickTimer JumpBufferTimer { get; set; }
    [Networked] private TickTimer WallJumpTimer { get; set; }

    private Vector2 slopeNormal;
    private const float coyoteTime = 0.15f;
    private const float jumpBufferTime = 0.1f;

    private void Awake()
    {
        networkTransform = GetComponent<NetworkTransform>();
        playerData = GetComponent<PlayerData>();

        if (groundCheck == null)
        {
            GameObject checkObj = new GameObject("GroundCheck");
            checkObj.transform.SetParent(transform);
            checkObj.transform.localPosition = new Vector3(0, -0.5f, 0);
            groundCheck = checkObj.transform;
        }

        if (wallCheck == null)
        {
            GameObject wallCheckObj = new GameObject("WallCheck");
            wallCheckObj.transform.SetParent(transform);
            wallCheckObj.transform.localPosition = new Vector3(0.3f, 0, 0);
            wallCheck = wallCheckObj.transform;
        }
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
            // Ground and slope checks
            CheckGround();
            CheckSlope();
            CheckWall();

            // Wall slide mechanics
            bool isWallSliding = IsTouchingWall && !IsGrounded && Velocity.y < 0;
            IsWallSliding = isWallSliding;

            // Update timers
            if (IsGrounded)
                CoyoteTimer = TickTimer.CreateFromSeconds(Runner, coyoteTime);

            if (input.jump)
                JumpBufferTimer = TickTimer.CreateFromSeconds(Runner, jumpBufferTime);

            // Horizontal movement
            Vector2 moveDirection = GetMoveDirection(input.movementInput.x);
            float targetSpeedX = input.movementInput.x * moveSpeed;
            float currentSpeedX = Velocity.x;

            // Check if we're in wall jump lockout period
            bool inWallJumpLockout = !WallJumpTimer.ExpiredOrNotRunning(Runner);

            if (Mathf.Abs(targetSpeedX) > 0.01f && !inWallJumpLockout)
            {
                // Accelerate
                float accel = acceleration;
                if (!IsGrounded)
                    accel *= airControl;

                currentSpeedX = Mathf.MoveTowards(currentSpeedX, targetSpeedX, accel * Runner.DeltaTime);
            }
            else if (!inWallJumpLockout)
            {
                // Apply friction
                float frictionForce = IsGrounded ? friction : friction * 0.5f;
                currentSpeedX = Mathf.MoveTowards(currentSpeedX, 0, frictionForce * Runner.DeltaTime);
            }

            Vector2 newVelocity = new Vector2(currentSpeedX, Velocity.y);

            // Wall jump
            if (!JumpBufferTimer.ExpiredOrNotRunning(Runner) && IsTouchingWall && !IsGrounded)
            {
                // Wall jump away from wall
                newVelocity.x = -WallDirection * moveSpeed * wallJumpDirection;
                newVelocity.y = wallJumpForce;

                JumpBufferTimer = TickTimer.None;
                WallJumpTimer = TickTimer.CreateFromSeconds(Runner, wallJumpTime);
            }
            // Regular jump
            else if (!JumpBufferTimer.ExpiredOrNotRunning(Runner) && !CoyoteTimer.ExpiredOrNotRunning(Runner))
            {
                // Jump perpendicular to slope if on slope
                if (OnSlope && IsGrounded)
                {
                    newVelocity = slopeNormal * jumpForce;
                }
                else
                {
                    newVelocity.y = jumpForce;
                }

                JumpBufferTimer = TickTimer.None;
                CoyoteTimer = TickTimer.None;
            }

            // Apply gravity
            if (!IsGrounded || !OnSlope)
            {
                float gravityMultiplier = 1f;
                if (newVelocity.y < 0)
                {
                    gravityMultiplier = fallGravityMultiplier;

                    // Slow fall when wall sliding
                    if (IsWallSliding)
                    {
                        newVelocity.y = Mathf.Max(newVelocity.y, -wallSlideSpeed);
                    }
                }
                else if (newVelocity.y > 0 && !input.jump)
                {
                    gravityMultiplier = lowJumpMultiplier;
                }

                if (!IsWallSliding)
                {
                    newVelocity.y += gravity * gravityMultiplier * Runner.DeltaTime;
                }
            }
            else
            {
                // On slope - apply downward force to stick to slope
                if (Mathf.Abs(input.movementInput.x) < 0.01f)
                {
                    newVelocity.y = -slopeDownForce;
                }
            }

            // Cap speeds
            newVelocity.x = Mathf.Clamp(newVelocity.x, -moveSpeed, moveSpeed);
            newVelocity.y = Mathf.Max(newVelocity.y, gravity * 2); // Terminal velocity

            Velocity = newVelocity;

            // Move the transform
            Vector3 movement;
            if (OnSlope && IsGrounded && Mathf.Abs(input.movementInput.x) > 0.01f)
            {
                // Move along slope
                movement = new Vector3(moveDirection.x * Mathf.Abs(Velocity.x), moveDirection.y * Mathf.Abs(Velocity.x), 0) * Runner.DeltaTime;
            }
            else
            {
                // Normal movement
                movement = new Vector3(Velocity.x, Velocity.y, 0) * Runner.DeltaTime;
            }

            transform.position += movement;

            // Align to slope rotation
            if (alignToSlope && IsGrounded && OnSlope)
            {
                float targetAngle = Mathf.Atan2(slopeNormal.y, slopeNormal.x) * Mathf.Rad2Deg - 90f;
                Quaternion targetRotation = Quaternion.Euler(0, 0, targetAngle);
                transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, rotationSpeed * Runner.DeltaTime);
            }
            else
            {
                // Return to upright
                transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.identity, rotationSpeed * Runner.DeltaTime);
            }

            // Check for ground collision to stop downward movement
            if (IsGrounded && Velocity.y < 0 && !OnSlope)
            {
                Velocity = new Vector2(Velocity.x, 0);
            }
        }
    }

    private void CheckGround()
    {
        IsGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer) != null;
    }

    private void CheckSlope()
    {
        // Raycast downward to check for slopes
        RaycastHit2D hit = Physics2D.Raycast(transform.position, Vector2.down, slopeCheckDistance, groundLayer);

        if (hit)
        {
            slopeNormal = hit.normal;
            SlopeAngle = Vector2.Angle(hit.normal, Vector2.up);

            OnSlope = SlopeAngle > 0.1f && SlopeAngle <= maxSlopeAngle;
        }
        else
        {
            slopeNormal = Vector2.up;
            SlopeAngle = 0f;
            OnSlope = false;
        }
    }

    private void CheckWall()
    {
        // Cast from the edges of the player
        Vector2 centerPos = transform.position;
        float checkOffset = 0.3f; // Adjust based on your player's width

        Vector2 rightCheckPos = centerPos + Vector2.right * checkOffset;
        Vector2 leftCheckPos = centerPos + Vector2.left * checkOffset;

        RaycastHit2D hitRight = Physics2D.Raycast(rightCheckPos, Vector2.right, wallCheckDistance, groundLayer);
        RaycastHit2D hitLeft = Physics2D.Raycast(leftCheckPos, Vector2.left, wallCheckDistance, groundLayer);

        if (hitRight)
        {
            IsTouchingWall = true;
            WallDirection = 1; // Wall is to the right
        }
        else if (hitLeft)
        {
            IsTouchingWall = true;
            WallDirection = -1; // Wall is to the left
        }
        else
        {
            IsTouchingWall = false;
            WallDirection = 0;
        }
    }

    private Vector2 GetMoveDirection(float horizontalInput)
    {
        if (!OnSlope || !IsGrounded)
        {
            return new Vector2(horizontalInput, 0).normalized;
        }

        // Calculate movement direction along slope
        return new Vector2(slopeNormal.y, -slopeNormal.x).normalized * Mathf.Sign(horizontalInput);
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            // Ground check visualization
            Gizmos.color = IsGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);

            // Slope check visualization
            Gizmos.color = OnSlope ? Color.yellow : Color.blue;
            Gizmos.DrawLine(transform.position, transform.position + Vector3.down * slopeCheckDistance);

            // Slope normal visualization
            if (OnSlope)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(transform.position, transform.position + new Vector3(slopeNormal.x, slopeNormal.y, 0) * 1f);
            }

            // Wall check visualization
            if (wallCheck != null)
            {
                Gizmos.color = IsTouchingWall ? Color.magenta : Color.gray;
                Gizmos.DrawLine(wallCheck.position, wallCheck.position + Vector3.right * wallCheckDistance);
                Gizmos.DrawLine(wallCheck.position, wallCheck.position + Vector3.left * wallCheckDistance);
            }
        }
    }
}