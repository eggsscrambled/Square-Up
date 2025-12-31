using UnityEngine;
using Fusion;

public class PlayerData : NetworkBehaviour
{
    [Networked]
    public int Health { get; set; }
    [Networked]
    public NetworkBool Dead { get; set; }
    [Networked]
    public int PlayerColorIndex { get; set; }
    [Networked]
    public int WeaponIndex { get; set; }

    private SpriteRenderer sprite;
    private bool tagAndLayerAssigned = false;

    private static readonly Color[] availableColors = new Color[]
    {
        Color.red,
        Color.blue,
        Color.green,
        new Color(0.5f, 0f, 0.5f)
    };

    private void Awake()
    {
        sprite = GetComponent<SpriteRenderer>();
    }

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            Health = 100;
            Dead = false;
            WeaponIndex = 0;
            PlayerColorIndex = Random.Range(0, availableColors.Length);
        }

        // Try to assign tag and layer immediately
        TryAssignTagAndLayer();

        ApplyColor();
    }

    public override void FixedUpdateNetwork()
    {
        // Try to assign tag and layer if not yet assigned
        if (!tagAndLayerAssigned)
        {
            TryAssignTagAndLayer();
        }

        ApplyColor();
    }

    private void TryAssignTagAndLayer()
    {
        if (!tagAndLayerAssigned && Object.HasInputAuthority)
        {
            bool success = true;

            // Assign layer
            int playerLayer = LayerMask.NameToLayer("Player");
            if (playerLayer != -1)
            {
                gameObject.layer = playerLayer;
                Debug.Log($"Layer 'Player' assigned to player {Object.InputAuthority}");
            }
            else
            {
                Debug.LogError("Layer 'Player' does not exist! Please add it in Project Settings → Tags and Layers.");
                success = false;
            }

            // Assign tag
            try
            {
                gameObject.tag = "Player";
                Debug.Log($"Tag 'Player' assigned to player {Object.InputAuthority}");
            }
            catch (UnityException e)
            {
                Debug.LogError($"Failed to assign tag 'Player': {e.Message}. Make sure 'Player' tag exists in Project Settings → Tags and Layers.");
                success = false;
            }

            tagAndLayerAssigned = success;
        }
    }

    private void ApplyColor()
    {
        if (sprite != null && PlayerColorIndex >= 0 && PlayerColorIndex < availableColors.Length)
        {
            sprite.color = availableColors[PlayerColorIndex];
        }
    }

    public void TakeDamage(int damage)
    {
        if (Object.HasStateAuthority)
        {
            Health -= damage;
            if (Health < 0)
            {
                Health = 0;
            }

            if (Health <= 0 && !Dead)
            {
                Dead = true;
                OnDeath();
            }
        }
    }

    private void OnDeath()
    {
        Debug.Log($"Player {Object.InputAuthority} has died!");
        if (Object.HasStateAuthority)
        {
            WeaponIndex = 0;
        }
    }

    public void Respawn()
    {
        if (Object.HasStateAuthority)
        {
            Health = 100;
            Dead = false;
            WeaponIndex = 0;
        }
    }

    public void PickupWeapon(int weaponIndex)
    {
        if (Object.HasStateAuthority)
        {
            WeaponIndex = weaponIndex;
        }
    }

    public void DropWeapon()
    {
        if (Object.HasStateAuthority)
        {
            WeaponIndex = 0;
        }
    }

    public bool HasWeapon()
    {
        return WeaponIndex > 0;
    }
}