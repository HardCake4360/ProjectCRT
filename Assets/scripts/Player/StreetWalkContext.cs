using TMPro;
using UnityEngine;

/// <summary>Connects the existing player and managers in the standalone street scene.</summary>
public sealed class StreetWalkContext : MonoBehaviour
{
    public PlayerControler player;
    public Transform streetSpawn;
    public Transform entranceOutside;
    public Transform entranceInside;
    public Bounds interiorBounds;
    public TMP_Text locationText;
    public TMP_Text controlsText;
    bool paused;
    bool bound;
    public bool IsInside => player != null && interiorBounds.Contains(player.transform.position);

    void Start() => Bind();
    void Bind()
    {
        if (player == null || MainLoop.Instance == null) return;
        MainLoop.Instance.PC = player;
        MainLoop.Instance.Cam = player.GetComponentInChildren<Camera>();
        MainLoop.Instance.SetMainLoopState(MainState.Main);
        bound = true;
        Resume();
    }
    void Update()
    {
        if (!bound) Bind();
        if (!bound) return;
        if (player == null || MainLoop.Instance == null) { bound = false; return; }
        if (ProjectCRT.Cutscenes.CutsceneDirector.IsActive)
        {
            if(locationText!=null)locationText.gameObject.SetActive(false);
            if(controlsText!=null)controlsText.gameObject.SetActive(false);
            return;
        }
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (NpcInvestigationController.CurrentInteraction != null)
            {
                var active = NpcInvestigationController.CurrentInteraction;
                if (DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueRunning) DialogueManager.Instance.ForceEndDia();
                active.CloseInteraction();
                return;
            }
            if (paused) Resume(); else Pause();
        }
        else if (paused && Application.isFocused && Input.GetMouseButtonDown(0)) Resume();
        if (player.transform.position.y < -5) ReturnToStreet();
        bool showWalkHud = paused || MainLoop.Instance.MainLoopState == MainState.Main;
        if (locationText != null) { locationText.gameObject.SetActive(showWalkHud); locationText.text = IsInside ? "샹그리아" : "거리"; }
        if (controlsText != null) { controlsText.gameObject.SetActive(showWalkHud); controlsText.text = paused ? "일시정지 · 클릭 또는 ESC로 계속" : "WASD 이동 · 마우스 시점 · E 대화 · ESC 일시정지"; }
    }
    void Pause()
    {
        // Do not override dialogue/interrogation states owned by the game.
        if (MainLoop.Instance == null || MainLoop.Instance.MainLoopState != MainState.Main) return;
        paused = true;
        MainLoop.Instance.SetMainLoopState(MainState.Freeze);
        player.HideHint();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
    void Resume()
    {
        if (MainLoop.Instance == null) return;
        if (!paused && MainLoop.Instance.MainLoopState != MainState.Main) return;
        paused = false;
        MainLoop.Instance.SetMainLoopState(MainState.Main);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
    void OnApplicationFocus(bool focused) { if (!focused && bound) Pause(); }
    public void ReturnToStreet()
    {
        if (player == null || streetSpawn == null) return;
        var controller = player.GetComponent<CharacterController>();
        bool wasEnabled = controller.enabled;
        controller.enabled = false;
        player.transform.SetPositionAndRotation(streetSpawn.position, streetSpawn.rotation);
        controller.enabled = wasEnabled;
    }
    void OnDisable()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
