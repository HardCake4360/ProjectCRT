using UnityEngine;

public struct AnimBool
{
    public string name;
    public bool value;
}

public class TitleAnimator : MonoBehaviour
{
    Animator anim;

    private void Start()
    {
        anim = GetComponent<Animator>();
    }

    public void SwitchAnimBool(string name)
    {
        bool value = anim.GetBool(name);
        anim.SetBool(name, !value);
    }

}
