using Fusion;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : NetworkBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 8f;
    [SerializeField] private float acceleration = 60f;
    [SerializeField] private float friction = 40f;

    [Header("Dash")]
    [SerializeField] private bool enableDash = false;
    [SerializeField] private float dashSpeed = 20f;
    [SerializeField] private float dashDuration = 0.2f;
    [SerializeField] private float dashCooldown = 1f;

    [Header("Recoil")]
    [SerializeField] private float recoilDecaySpeed = 10f;
    [SerializeField] private float collisionRadius = 0.5f;

    private Rigidbody2D _rb;
    private PlayerData _playerData;

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
        if (_playerData != null && _playerData.Dead)
        {
            _rb.linearVelocity = Vector2.zero;
            return;
        }

        if (GetInput(out NetworkInputData input))
        {
            Vector2 currentVelocity = Velocity;
            bool isDashing = !DashTimer.ExpiredOrNotRunning(Runner);
            IsDashing = isDashing;

            if (isDashing)
            {
                currentVelocity = DashDirection * dashSpeed;
            }
            else
            {
                // Corrected Dash Button Access for Fusion 2
                if (enableDash && input.buttons.IsSet(MyButtons.Dash) && DashCooldownTimer.ExpiredOrNotRunning(Runner) && input.movementInput.magnitude > 0.1f)
                {
                    DashDirection = input.movementInput.normalized;
                    DashTimer = TickTimer.CreateFromSeconds(Runner, dashDuration);
                    DashCooldownTimer = TickTimer.CreateFromSeconds(Runner, dashCooldown);
                    currentVelocity = DashDirection * dashSpeed;
                }
                else
                {
                    Vector2 targetVelocity = input.movementInput * moveSpeed;
                    if (input.movementInput.magnitude > 0.01f)
                        currentVelocity = Vector2.MoveTowards(currentVelocity, targetVelocity, acceleration * Runner.DeltaTime);
                    else
                        currentVelocity = Vector2.MoveTowards(currentVelocity, Vector2.zero, friction * Runner.DeltaTime);
                }
            }

            RecoilVelocity = Vector2.Lerp(RecoilVelocity, Vector2.zero, recoilDecaySpeed * Runner.DeltaTime);
            Velocity = currentVelocity;
            _rb.linearVelocity = Velocity + RecoilVelocity;
        }
    }

    // FIXES COMPILER ERROR IN NetworkedGameUIManager
    public float GetDashCooldownProgress()
    {
        if (Runner == null || DashCooldownTimer.ExpiredOrNotRunning(Runner)) return 0f;
        float remainingTime = DashCooldownTimer.RemainingTime(Runner) ?? 0f;
        return Mathf.Clamp01(remainingTime / dashCooldown);
    }

    public void ApplyRecoil(Vector2 recoilForce)
    {
        if (Object.HasStateAuthority || Object.HasInputAuthority)
            RecoilVelocity += recoilForce;
    }
}