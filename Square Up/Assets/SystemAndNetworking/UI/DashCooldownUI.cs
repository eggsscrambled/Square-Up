using UnityEngine;
using UnityEngine.UI;
using Fusion;

public class DashCooldownUI : NetworkBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject dashUIContainer;
    [SerializeField] private Image dashCooldownImage;

    [Header("Settings")]
    [SerializeField] private bool hideWhenReady = true;
    [SerializeField] private Color cooldownColor = Color.gray;
    [SerializeField] private Color readyColor = Color.white;

    private PlayerController _playerController;
    private bool _isLocalPlayer = false;

    public override void Spawned()
    {
        _playerController = GetComponent<PlayerController>();

        // Check if this is the local player (has input authority)
        _isLocalPlayer = Object.HasInputAuthority;

        // Only show UI for local player
        if (dashUIContainer != null)
        {
            dashUIContainer.SetActive(_isLocalPlayer);
        }
    }

    public override void Render()
    {
        // Only update if this is the local player and UI exists
        if (!_isLocalPlayer || dashUIContainer == null || dashCooldownImage == null || _playerController == null)
            return;

        // Get cooldown progress (0 = ready, 1 = just used)
        float cooldownProgress = _playerController.GetDashCooldownProgress();

        // Update fill amount (reversed: empties when used, fills up as it cools down)
        dashCooldownImage.fillAmount = 1f - cooldownProgress;

        // Optional: Change color based on ready state
        dashCooldownImage.color = cooldownProgress > 0 ? cooldownColor : readyColor;

        // Optional: Hide UI when dash is ready
        if (hideWhenReady)
        {
            dashUIContainer.SetActive(cooldownProgress > 0);
        }
    }
}