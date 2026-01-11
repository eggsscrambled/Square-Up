using UnityEngine;
using Fusion;

/// <summary>
/// A Fusion 2 NetworkBehaviour that handles player teleportation.
/// State is synced via the Networked property, and effects are triggered
/// globally via a ChangeDetector in the Render loop.
/// </summary>
public class Portal : NetworkBehaviour
{
    [Header("Settings")]
    public Transform teleportPos;
    public ParticleSystem particles;
    public AudioSource source;
    public AudioClip portalSFX;

    [Networked]
    private int TeleportCount { get; set; }

    private ChangeDetector _changeDetector;

    public override void Spawned()
    {
        // Initialize the change detector to track networked state changes
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
    }

    /// <summary>
    /// Render is called every frame and is the best place for local-only visual/audio updates
    /// based on networked state in Fusion 2.
    /// </summary>
    public override void Render()
    {
        // Check for changes in networked properties
        foreach (var change in _changeDetector.DetectChanges(this))
        {
            switch (change)
            {
                case nameof(TeleportCount):
                    PlayPortalEffects();
                    break;
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // Only the StateAuthority (usually the Server/Host) should process the logic
        // to ensure the teleportation is authoritative and synced.
        if (!HasStateAuthority) return;

        if (collision.CompareTag("Player"))
        {
            TeleportPlayer(collision);
        }
    }

    private void TeleportPlayer(Collider2D collider)
    {
        // Fusion 2: If the player is a NetworkObject, we manipulate its transform.
        // If you are using NetworkCharacterController, you should use cc.Teleport instead.
        Transform playerTransform = collider.transform;

        // Calculate offset relative to the entry portal
        Vector2 offset = (Vector2)playerTransform.position - (Vector2)transform.position;

        // Calculate destination orientation logic (Simplified from original)
        Vector2 destBase = teleportPos.position;

        // Apply the teleportation
        playerTransform.position = destBase + offset;

        // Increment the networked counter. 
        // This change will be detected by all clients in their Render() loop.
        TeleportCount++;
    }

    private void PlayPortalEffects()
    {
        if (particles != null)
        {
            particles.Play();
        }

        if (source != null && portalSFX != null)
        {
            source.PlayOneShot(portalSFX);
        }
    }
}