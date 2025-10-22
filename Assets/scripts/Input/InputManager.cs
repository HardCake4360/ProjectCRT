using UnityEngine;

public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }

    [Header("대사 진행 키 설정")]
    public KeyCode[] dialogueAdvanceKeys = new KeyCode[] { KeyCode.Space, KeyCode.Return, KeyCode.Mouse0 };
    [Header("상호작용 키 설정")]
    public KeyCode[] interactionKeys = new KeyCode[] { KeyCode.E, KeyCode.Mouse0 };

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject); // 씬 전환에도 유지
    }

    public bool IsAnyKeyPressedIn(KeyCode[] keys)
    {
        foreach (var key in keys)
        {
            if (Input.GetKeyDown(key))
                return true;
        }
        return false;
    }
}
