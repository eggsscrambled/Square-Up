using Fusion;
using UnityEngine;

public struct NetworkInputData : INetworkInput
{
    public Vector2 movementInput;
    public Vector2 aimDirection;
    public Vector2 mouseWorldPosition;
    public NetworkBool fire;
    public NetworkBool wasFirePressedLastTick;
    public NetworkBool pickup; // This will now act as a reliable pulse
    public NetworkBool dash;   // This will now act as a reliable pulse
}

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : NetworkBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 8f;
    [SerializeField] private float acceleration = 60f; // Snappier for 64 TPS
    [SerializeField] private float friction = 40f;

    [Header("Rotation")]
    [SerializeField] private bool rotateTowardsMovement = false;
    [SerializeField] private bool rotateTowardsAim = true;
    [SerializeField] private float rotationSpeed = 15f;

    [Header("Dash (Optional)")]
    [SerializeField] private bool enableDash = false;
    [SerializeField] private float dashSpeed = 20f;
    [SerializeField] private float dashDuration = 0.2f;
    [SerializeField] private float dashCooldown = 1f;

    [Header("Recoil")]
    [SerializeField] private float recoilDecaySpeed = 10f;

    [Header("Collision")]
    [SerializeField] private LayerMask obstacleLayer;
    [SerializeField] private float collisionRadius = 0.5f;

    private Rigidbody2D _rb;
    private PlayerData _playerData;

    // Networked state
    [Networked] private Vector2 Velocity { get; set; }
    [Networked] private Vector2 RecoilVelocity { get; set; }
    [Networked] private TickTimer DashTimer { get; set; }
    [Networked] private TickTimer DashCooldownTimer { get; set; }
    [Networked] private Vector2 DashDirection { get; set; }
    [Networked] private NetworkBool IsDashing { get; set; }

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _playerData = GetComponent<PlayerData>();
    }

    public override void FixedUpdateNetwork()
    {
        // Don't allow movement if dead
        if (_playerData != null && _playerData.Dead)
        {
            _rb.linearVelocity = Vector2.zero;
            return;
        }

        // IMPORTANT: We do NOT return if !HasStateAuthority here. 
        // Prediction requires this block to run on the Input Authority too.

        if (GetInput(out NetworkInputData input))
        {
            Vector2 currentVelocity = Velocity;

            // 1. Dash Logic
            bool isDashing = !DashTimer.ExpiredOrNotRunning(Runner);
            IsDashing = isDashing;

            if (isDashing)
            {
                currentVelocity = DashDirection * dashSpeed;
            }
            else
            {
                // Start dash pulse (from LobbyManager accumulator)
                if (enableDash && input.dash && DashCooldownTimer.ExpiredOrNotRunning(Runner) && input.movementInput.magnitude > 0.1f)
                {
                    DashDirection = input.movementInput.normalized;
                    DashTimer = TickTimer.CreateFromSeconds(Runner, dashDuration);
                    DashCooldownTimer = TickTimer.CreateFromSeconds(Runner, dashCooldown);
                    currentVelocity = DashDirection * dashSpeed;
                }
                else
                {
                    // 2. Normal Movement (Acceleration and Friction)
                    Vector2 targetVelocity = input.movementInput * moveSpeed;

                    if (input.movementInput.magnitude > 0.01f)
                        currentVelocity = Vector2.MoveTowards(currentVelocity, targetVelocity, acceleration * Runner.DeltaTime);
                    else
                        currentVelocity = Vector2.MoveTowards(currentVelocity, Vector2.zero, friction * Runner.DeltaTime);

                    if (currentVelocity.magnitude > moveSpeed)
                        currentVelocity = currentVelocity.normalized * moveSpeed;
                }
            }

            // 3. Recoil Decay
            RecoilVelocity = Vector2.Lerp(RecoilVelocity, Vector2.zero, recoilDecaySpeed * Runner.DeltaTime);

            // Save state for the network
            Velocity = currentVelocity;

            // 4. Physics Engine Interaction
            // We set the Rigidbody velocity. NetworkRigidbody2D will handle 
            // the transform positioning and client-side prediction.
            _rb.linearVelocity = Velocity + RecoilVelocity;

            // 5. Handle Rotation (Interpolated transform)
            HandleRotation(input);
        }
    }

    private void HandleRotation(NetworkInputData input)
    {
        Quaternion targetRotation = transform.rotation;

        if (rotateTowardsAim && input.aimDirection.magnitude > 0.1f)
        {
            float targetAngle = Mathf.Atan2(input.aimDirection.y, input.aimDirection.x) * Mathf.Rad2Deg - 90f;
            targetRotation = Quaternion.Euler(0, 0, targetAngle);
        }
        else if (rotateTowardsMovement && input.movementInput.magnitude > 0.1f)
        {
            float targetAngle = Mathf.Atan2(input.movementInput.y, input.movementInput.x) * Mathf.Rad2Deg - 90f;
            targetRotation = Quaternion.Euler(0, 0, targetAngle);
        }

        transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, rotationSpeed * Runner.DeltaTime);
    }

    public void ApplyRecoil(Vector2 recoilForce)
    {
        // Allow Input Authority to predict recoil for instant feel
        if (Object.HasStateAuthority || Object.HasInputAuthority)
        {
            RecoilVelocity += recoilForce;
        }
    }

    public float GetDashCooldownProgress()
    {
        if (Runner == null || DashCooldownTimer.ExpiredOrNotRunning(Runner)) return 0f;
        float remainingTime = DashCooldownTimer.RemainingTime(Runner) ?? 0f;
        return Mathf.Clamp01(remainingTime / dashCooldown);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, collisionRadius);

        if (Application.isPlaying)
        {
            Gizmos.color = IsDashing ? Color.yellow : Color.green;
            Gizmos.DrawLine(transform.position, transform.position + (Vector3)Velocity);

            if (RecoilVelocity.magnitude > 0.01f)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(transform.position, transform.position + (Vector3)RecoilVelocity);
            }
        }
    }
}