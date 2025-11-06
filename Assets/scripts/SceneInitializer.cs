using UnityEngine;
using UnityEngine.Events;
using System.Collections;

public class SceneInitializer : MonoBehaviour
{
    public UnityEvent OnStart;
    public UnityEvent OnInitial;
    public float Delay;

    [Header("Initial set")]
    public PlayerControler PC;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        OnStart?.Invoke();
        StartCoroutine(initializeScene());
    }

    IEnumerator initializeScene()
    {
        yield return new WaitForSeconds(Delay);
        OnInitial?.Invoke();
    }
    
    public void OuterSceneInitPlayer()
    {
        MainLoop.Instance.PC = this.PC;
    }

}
