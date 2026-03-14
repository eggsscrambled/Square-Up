using UnityEngine;
using UnityEngine.UI;
using Fusion;

public class ReloadAmmoUI : NetworkBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject reloadUIContainer;
    [SerializeField] private Image reloadFillImage;

    [Header("Settings")]
    [SerializeField] private bool hideWhenFull = false;
    [SerializeField] private Color reloadingColor = Color.yellow;
    [SerializeField] private Color ammoColor = Color.white;
    [SerializeField] private Color lowAmmoColor = Color.red;
    [SerializeField, Range(0f, 0.5f)] private float lowAmmoThreshold = 0.3f;

    private WeaponAimController _weaponAimController;
    private bool _isLocalPlayer = false;

    public override void Spawned()
    {
        _weaponAimController = GetComponent<WeaponAimController>();

        // Check if this is the local player (has input authority)
        _isLocalPlayer = Object.HasInputAuthority;

        // Only show UI for local player
        if (reloadUIContainer != null)
        {
            reloadUIContainer.SetActive(_isLocalPlayer);
        }
    }

    public override void Render()
    {
        // Only update if this is the local player and UI exists
        if (!_isLocalPlayer || reloadUIContainer == null || reloadFillImage == null || _weaponAimController == null)
            return;

        // Check if we have a weapon
        WeaponPickup currentWeapon = _weaponAimController.CurrentWeapon;
        if (currentWeapon == null)
        {
            reloadUIContainer.SetActive(false);
            return;
        }

        WeaponData weaponData = currentWeapon.GetWeaponData();
        if (weaponData == null)
        {
            reloadUIContainer.SetActive(false);
            return;
        }

        // Check if currently reloading
        bool isReloading = !_weaponAimController.reloadTimer.ExpiredOrNotRunning(Runner);

        float fillAmount;
        Color fillColor;

        if (isReloading)
        {
            // During reload: show reload progress (0 to 1)
            float reloadTimeRemaining = _weaponAimController.reloadTimer.RemainingTime(Runner) ?? 0f;
            float reloadProgress = 1f - (reloadTimeRemaining / weaponData.reloadTimeSeconds);
            fillAmount = Mathf.Clamp01(reloadProgress);
            fillColor = reloadingColor;
        }
        else
        {
            // Not reloading: show ammo ratio
            int currentAmmo = _weaponAimController.RemainingAmmo;
            int maxAmmo = weaponData.maxAmmo;

            // Cast to float to avoid integer division
            fillAmount = maxAmmo > 0 ? (float)currentAmmo / (float)maxAmmo : 0f;

            // Color based on ammo level
            if (fillAmount <= lowAmmoThreshold)
            {
                fillColor = lowAmmoColor;
            }
            else
            {
                fillColor = ammoColor;
            }
        }

        // Update the UI
        reloadFillImage.fillAmount = fillAmount;
        reloadFillImage.color = fillColor;

        // Optional: Hide when full and not reloading
        if (hideWhenFull && !isReloading)
        {
            reloadUIContainer.SetActive(fillAmount < 1f);
        }
        else
        {
            reloadUIContainer.SetActive(true);
        }
    }
}