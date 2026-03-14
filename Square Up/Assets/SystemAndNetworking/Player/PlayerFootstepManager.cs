using UnityEngine;
using Fusion;

[RequireComponent(typeof(PlayerController))]
public class PlayerFootstepManager : MonoBehaviour
{
    [Header("Footstep Settings")]
    [SerializeField] private AudioClip[] footstepSounds;
    [SerializeField] private float stepInterval = 0.4f;
    [SerializeField] private float minSpeedForFootsteps = 0.5f;
    [SerializeField] private float volumeScale = 0.6f;

    [Header("Pitch Variation")]
    [SerializeField] private float minPitch = 0.9f;
    [SerializeField] private float maxPitch = 1.1f;

    private AudioSource _audioSource;
    private PlayerController _controller;
    private PlayerData _playerData;
    private float _stepTimer;

    private void Awake()
    {
        _controller = GetComponent<PlayerController>();
        _playerData = GetComponent<PlayerData>();

        // Create a local AudioSource for footsteps
        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.spatialBlend = 1f; // Full 3D sound
        _audioSource.minDistance = 1f;
        _audioSource.maxDistance = 15f;
        _audioSource.volume = volumeScale;
        _audioSource.playOnAwake = false;
    }

    private void Update()
    {
        // Don't play footsteps if dead or no sounds assigned
        if (_playerData != null && _playerData.Dead) return;
        if (footstepSounds == null || footstepSounds.Length == 0) return;

        // Get velocity from Rigidbody2D (this is synchronized via NetworkTransform)
        Rigidbody2D rb = _controller.GetComponent<Rigidbody2D>();
        float currentSpeed = rb.linearVelocity.magnitude;

        // Only play footsteps if moving above threshold
        if (currentSpeed > minSpeedForFootsteps)
        {
            _stepTimer -= Time.deltaTime;

            if (_stepTimer <= 0f)
            {
                PlayFootstep();

                // Adjust step interval based on speed (faster = quicker steps)
                float speedRatio = currentSpeed / _controller.GetMoveSpeed();
                _stepTimer = stepInterval / Mathf.Max(speedRatio, 1f);
            }
        }
        else
        {
            _stepTimer = 0f; // Reset when stopped
        }
    }

    private void PlayFootstep()
    {
        if (footstepSounds.Length == 0) return;

        // Pick random footstep sound
        AudioClip clip = footstepSounds[Random.Range(0, footstepSounds.Length)];

        // Randomize pitch for variation
        _audioSource.pitch = Random.Range(minPitch, maxPitch);

        _audioSource.PlayOneShot(clip);
    }
}