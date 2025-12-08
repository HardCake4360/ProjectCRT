using UnityEngine;

public class Phone : MonoBehaviour
{
    Animator anim;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        anim = GetComponent<Animator>();
    }

    public void OnCall()
    {
        anim.Play("PhoneRinging");
    }

    public void OnRespond()
    {
        anim.Play("Idle");
        AudioManager.Instance.PlaySFX("phonePickUp");
    }

    //애니메이션 함수
    public void Ani_PhoneRingingSFX()
    {
        AudioManager.Instance.PlaySFX("phoneRing");
    }
}
