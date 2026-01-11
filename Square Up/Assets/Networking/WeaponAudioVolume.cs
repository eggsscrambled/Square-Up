using UnityEngine;

public class WeaponAudioVolume : MonoBehaviour
{
    private AudioSource audioSource;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();

        if (audioSource != null)
        {
            float weaponVolume = PlayerPrefs.GetFloat(Settings.WEAPON_VOLUME_KEY, 1f);
            audioSource.volume = weaponVolume;
        }
    }
}