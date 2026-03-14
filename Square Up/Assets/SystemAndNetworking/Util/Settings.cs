using UnityEngine;
using UnityEngine.UI;

public class Settings : MonoBehaviour
{
    public AudioSource menuMusicSource;

    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider weaponSlider;
    [SerializeField] private Slider miscSlider;

    public const string MUSIC_VOLUME_KEY = "MusicVolume";
    public const string WEAPON_VOLUME_KEY = "WeaponVolume";
    public const string MISC_VOLUME_KEY = "MiscVolume";

    private void Start()
    {
        LoadVolumes();

        // Add listeners to save when slider value changes
        musicSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
        weaponSlider.onValueChanged.AddListener(OnWeaponVolumeChanged);
        miscSlider.onValueChanged.AddListener(OnMiscVolumeChanged);
    }

    private void LoadVolumes()
    {
        // Load saved volumes or use default value of 1.0
        float musicVol = PlayerPrefs.GetFloat(MUSIC_VOLUME_KEY, 1f);
        float weaponVol = PlayerPrefs.GetFloat(WEAPON_VOLUME_KEY, 1f);
        float miscVol = PlayerPrefs.GetFloat(MISC_VOLUME_KEY, 1f);

        // Set slider values without triggering the listener
        musicSlider.SetValueWithoutNotify(musicVol);
        weaponSlider.SetValueWithoutNotify(weaponVol);
        miscSlider.SetValueWithoutNotify(miscVol);

        // Apply music volume to menu music source
        if (menuMusicSource != null)
        {
            menuMusicSource.volume = musicVol;
        }
    }

    private void OnMusicVolumeChanged(float value)
    {
        PlayerPrefs.SetFloat(MUSIC_VOLUME_KEY, value);
        PlayerPrefs.Save();

        // Apply the volume to menu music source
        if (menuMusicSource != null)
        {
            menuMusicSource.volume = value;
        }
    }

    private void OnWeaponVolumeChanged(float value)
    {
        PlayerPrefs.SetFloat(WEAPON_VOLUME_KEY, value);
        PlayerPrefs.Save();

        // Apply the volume to your audio system here
        // Example: AudioManager.Instance.SetWeaponVolume(value);
    }

    private void OnMiscVolumeChanged(float value)
    {
        PlayerPrefs.SetFloat(MISC_VOLUME_KEY, value);
        PlayerPrefs.Save();

        // Apply the volume to your audio system here
        // Example: AudioManager.Instance.SetMiscVolume(value);
    }

    private void OnDestroy()
    {
        // Clean up listeners
        musicSlider.onValueChanged.RemoveListener(OnMusicVolumeChanged);
        weaponSlider.onValueChanged.RemoveListener(OnWeaponVolumeChanged);
        miscSlider.onValueChanged.RemoveListener(OnMiscVolumeChanged);
    }
}