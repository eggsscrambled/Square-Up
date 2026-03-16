using UnityEngine;

public class URL : MonoBehaviour
{

    public string url;
public void pressedURL()
    {
        Application.OpenURL(url);
    }
}
