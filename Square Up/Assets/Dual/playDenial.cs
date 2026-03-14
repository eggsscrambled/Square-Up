using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class playDenial : MonoBehaviour
{
    public TMP_InputField LobbyInput;
    public GameObject oldPanel;
    public GameObject newPanel;
    public Animator animator;
    public void NextPanel()
    {

        if (!string.IsNullOrEmpty(LobbyInput.text.Trim()))
        {
            oldPanel.SetActive(false);
            newPanel.SetActive(true);
        }
        else
        {
            animator.SetBool("Go", true);
            StartCoroutine(delay());
        }


    }

    public IEnumerator delay()
    {
        yield return new WaitForSeconds(0.5f);
        animator.SetBool("Go", false);
    }
}
