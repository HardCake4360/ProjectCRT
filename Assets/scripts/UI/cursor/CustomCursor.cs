using UnityEngine;

public enum CursorState
{
    Default,
    Loading,
    Text,
    Select,
    ResizeHorizontal,
    ResizeVertical,
    ResizeMainDiagonal,
    ResizeAntiDiagonal
}

public class CustomCursor : MonoBehaviour
{
    public static CustomCursor Instance { get; private set; }

    [SerializeField]
    private Texture2D
        Default,
        Loading,
        Text,
        Select,
        ResizeHorizontal,
        ResizeVertical,
        ResizeMainDiagonal,
        ResizeAntiDiagonal;

    private Texture2D currentCursor;
    private Vector2 cursorHotspot = Vector2.zero;
    private Vector2 hotspotMiddle;

    private CursorMode cursorMode = CursorMode.Auto;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        hotspotMiddle = new Vector2(Default.width / 2, Default.height / 2);

        currentCursor = Default;
        Cursor.SetCursor(currentCursor, cursorHotspot, cursorMode);
    }

    public void SetCursorSprite(CursorState cursor)
    {
        switch (cursor)
        {
            case CursorState.Default:
                cursorHotspot = Vector2.zero;
                currentCursor = Default;
                break;

            case CursorState.Loading:
                cursorHotspot = Vector2.zero;
                currentCursor = Loading;
                break;

            case CursorState.Text:
                cursorHotspot = hotspotMiddle;
                currentCursor = Text;
                break;

            case CursorState.Select:
                cursorHotspot = Vector2.zero;
                currentCursor = Select;
                break;

            case CursorState.ResizeHorizontal:
                cursorHotspot = hotspotMiddle;
                currentCursor = ResizeHorizontal;
                break;

            case CursorState.ResizeVertical:
                cursorHotspot = hotspotMiddle;
                currentCursor = ResizeVertical;
                break;

            case CursorState.ResizeMainDiagonal:
                cursorHotspot = hotspotMiddle;
                currentCursor = ResizeMainDiagonal;
                break;

            case CursorState.ResizeAntiDiagonal:
                cursorHotspot = hotspotMiddle;
                currentCursor = ResizeAntiDiagonal;
                break;
        }

        Cursor.SetCursor(currentCursor, cursorHotspot, cursorMode);
    }

    public void SetCursorDefault()
    {
        cursorHotspot = Vector2.zero;
        Cursor.SetCursor(Default, cursorHotspot, cursorMode);
    }
}
