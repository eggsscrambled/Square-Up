using UnityEngine;

public class BasicUINav : MonoBehaviour
{

    public GameObject oldPanel;
    public GameObject newPanel;
    public void NextPanel()
    {
        oldPanel.SetActive(false);
        newPanel.SetActive(true);
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}
