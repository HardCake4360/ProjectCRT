using UnityEngine;

public class MainLoop : MonoBehaviour
{
    public static MainLoop Instance { get; private set; }

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
