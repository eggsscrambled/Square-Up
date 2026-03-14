using UnityEngine;

public class ColorEnabler : MonoBehaviour
{
    public ColorManager colorManager;
    public void OnUsedPress()
    {
        for (int i = 0; i < colorManager.Outlines.Length ; i++)
        { 
            colorManager.Outlines[i].SetActive(false);
        }

        colorManager.idenity.outlineOBJ.SetActive(true);
    }
}
