using UnityEngine;
using System.Collections;

public class MainLoop : MonoBehaviour
{
    public static MainLoop Instance { get; private set; }

    public DialogueObject startDiaEvent;

    private void Awake()
    {
        if(Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
    private void Start()
    {
        StartCoroutine(startEvnet());
    }

    IEnumerator startEvnet()
    {
        yield return new WaitForSeconds(2f);
        DialogueManager.Instance.DialogueEventTrigger(startDiaEvent);
    }

    public MemoPost memo;
    public UIManager UI;
    public bool posting;
    public Camera cam;

    // Update is called once per frame
    void Update()
    {
        if(posting) memo.PostMode();
    }
}
