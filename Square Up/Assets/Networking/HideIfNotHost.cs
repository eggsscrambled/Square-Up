using UnityEngine;
using Fusion;

public class HideIfNotHost : MonoBehaviour
{
    void Start()
    {
        StartCoroutine(CheckAndHide());
    }

    private System.Collections.IEnumerator CheckAndHide()
    {
        // Wait a frame to ensure NetworkRunner is initialized
        yield return null;

        var runner = FindObjectOfType<NetworkRunner>();

        if (runner == null || !runner.IsServer)
        {
            gameObject.SetActive(false);
        }
    }
}