using UnityEngine;

public class DynamicMusicManager : MonoBehaviour
{
    public static DynamicMusicManager Instance;

    [Header("Components")]
    public AudioSource musicSource;
    public AudioLowPassFilter lowPassFilter;

    [Header("Volume Settings")]
    public float minVolume = 0.01f;
    public float maxVolume = 1.0f;
    public float volumePerShot = 0.1f;
    public float decayRate = 0.4f; // Recovery speed

    [Header("Filter Settings")]
    public float minFrequency = 500f;   // Muffled/Underwater
    public float maxFrequency = 22000f; // Full clarity

    [Header("Pitch Settings")]
    public float normalPitch = 1.0f;
    public float maxPitch = 1.15f; // Higher = more intense
    public float pitchThreshold = 0.75f; // Start pitching up at 75% intensity

    private float _currentIntensity = 0f;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (lowPassFilter == null) lowPassFilter = GetComponent<AudioLowPassFilter>();
    }

    private void Update()
    {
        // Slowly decay the intensity over time
        if (_currentIntensity > 0)
        {
            _currentIntensity -= decayRate * Time.deltaTime;
            _currentIntensity = Mathf.Clamp01(_currentIntensity);
        }

        // Apply Volume Lerp
        float targetVol = Mathf.Lerp(minVolume, maxVolume, _currentIntensity);
        musicSource.volume = Mathf.MoveTowards(musicSource.volume, targetVol, Time.deltaTime);

        // Apply Frequency Lerp (The "Muffle" effect)
        if (lowPassFilter != null)
        {
            lowPassFilter.cutoffFrequency = Mathf.Lerp(minFrequency, maxFrequency, _currentIntensity);
        }

        // Apply Pitch Shift in the top 25% of intensity range
        if (_currentIntensity >= pitchThreshold)
        {
            // Map 0.75-1.0 intensity to 0-1 for pitch lerp
            float pitchT = (_currentIntensity - pitchThreshold) / (1f - pitchThreshold);
            musicSource.pitch = Mathf.Lerp(normalPitch, maxPitch, pitchT);
        }
        else
        {
            musicSource.pitch = normalPitch;
        }
    }

    public void RegisterShot()
    {
        _currentIntensity += volumePerShot;
        _currentIntensity = Mathf.Clamp01(_currentIntensity);
    }
}