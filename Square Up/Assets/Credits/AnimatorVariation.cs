using UnityEngine;

public class AnimatorVariation : MonoBehaviour
{

    public Animator animatorOfGun;
    public float speed;

    void Start()
    {
        animatorOfGun.speed = speed;
    }

}
