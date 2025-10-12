using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public bool isUIActive;
    [SerializeField] private float transSpeed;
    [SerializeField] HiddenUI[] UIs;
    private Animator anim;
    SceneLoader sceneLoader;

    [SerializeField] ReactiveButton b1;
    [SerializeField] ReactiveButton b2;
    [SerializeField] ReactiveButton b3;

    private void Start()
    {
        anim = GetComponent<Animator>();
        sceneLoader = SceneLoader.Instance;
        b1.OnClick.AddListener(() => sceneLoader.LoadScene("testMap"));
        b2.OnClick.AddListener(() => sceneLoader.LoadScene("testMap"));
        b3.OnClick.AddListener(() => sceneLoader.LoadScene("testMap"));
    }

    public void ToggleByIdx(int i,bool show)
    {
        StartCoroutine(UIs[i].MoveRoutine(show));
    }

    public void EnableShow()
    {
        anim.SetBool("show", true);
    }
    public void DisableShow()
    {
        anim.SetBool("show", false);
    }
}
