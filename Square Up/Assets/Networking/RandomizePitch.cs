using System.Xml.Serialization;
using UnityEngine;

public class RandomizePitch : MonoBehaviour
{
    public float min;
    public float max;

    private void Awake()
    {
        AudioSource source = GetComponent<AudioSource>();
        source.pitch = Random.Range(min, max);
    }
}