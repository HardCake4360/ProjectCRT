using UnityEngine;
using TMPro;

public class PlayerControler : MonoBehaviour
{
    [System.NonSerialized] public bool cameraTransitionInProgress;
    [Header("기본 물리설정")]
    CharacterController charCon;
    [SerializeField] GameObject cam;
    [SerializeField] float speed;
    [SerializeField] float sensitivity;
    [SerializeField] float cameraVerticalClamp;

    [Header("Raycast 설정")]
    [SerializeField] private Transform rayOrigin;
    [SerializeField] private float rayDistance = 3.0f;
    [SerializeField] TextMeshProUGUI hintText;

    private float horizontalInput;
    private float verticalInput;

    private float xRotation;//수직회전
    private Vector3 velocity;

    private RaycastHit hitInfo;//레이캐스트 힛 오브젝트 정보

    [Header("플레이어 속성")]
    private bool nothing;//필요없는 속성 지웠는데 헤더는 남겨놓고 싶어서 아무거나 써놓음

    private void Start()
    {
        charCon = GetComponent<CharacterController>();
        horizontalInput = 0f;
        verticalInput   = 0f;
        Cursor.lockState = CursorLockMode.Locked;
    }

    public void UpdatePlayer()
    {
        if (cameraTransitionInProgress) { HideHint(); return; }
        //- - - - - - - - 이동 로직 - - - - - - - - 
        horizontalInput = Input.GetAxis("Horizontal");
        verticalInput = Input.GetAxis("Vertical");

        velocity = GetMoveVector();
        if(velocity.magnitude > 0.05f)
        {
            charCon.Move(velocity * Time.deltaTime);
        }

        float mouseX = Input.GetAxis("Mouse X") * sensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * sensitivity * Time.deltaTime;

        xRotation -= mouseY;

        xRotation = Mathf.Clamp(xRotation, -cameraVerticalClamp, cameraVerticalClamp);
        cam.transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        gameObject.transform.Rotate(Vector3.up * mouseX);

        //- - - - - - - - 상호작용 - - - - - - - -
        //rayOrigin에서 전방으로 레이를 발사
        if (TryGetInteractionHit(out hitInfo))
        {
            // 디버깅 용으로 레이를 시각화
            Debug.DrawRay(rayOrigin.position, rayOrigin.forward * rayDistance, Color.green);
            
            //Interactable 인터페이스 검색
            Interactable interactableObject = hitInfo.collider.GetComponentInParent<Interactable>();
            if (hintText != null)
            {
                hintText.enabled = interactableObject != null && interactableObject.IsInteractionAllowed;
                if (hintText.enabled) hintText.text = interactableObject.HintName + " [E]";
            }

            if (InputManager.Instance != null && InputManager.Instance.IsAnyKeyPressedIn(InputManager.Instance.interactionKeys))
            {
                if (interactableObject != null && interactableObject.IsInteractionAllowed)
                {
                    TryInteractInView();
                    Debug.Log($"상호작용: {hitInfo.collider.gameObject.name}");
                }
                else
                {
                    // Interactable이 없는 오브젝트와 충돌했습니다.
                    Debug.Log($"{hitInfo.collider.gameObject.name}는 상호작용 불가 객체입니다.");
                }
            }
        }
        else
        {
            // 레이가 아무것도 충돌하지 않았을 때
            Debug.DrawRay(rayOrigin.position, rayOrigin.forward * rayDistance, Color.red);
            if(hintText) hintText.enabled = false;
        }

    }

    public Vector3 GetMoveVector()
    {
        Vector3 vel = (transform.forward * verticalInput) + (transform.right * horizontalInput);
        return new Vector3(vel.x, -9.8f, vel.z) * speed;
    }

    public float ViewPitch => xRotation;

    public void RestoreWalkingPose(Vector3 position, float yaw, float pitch)
    {
        var controller = GetComponent<CharacterController>();
        bool wasEnabled = controller != null && controller.enabled;
        if (controller != null) controller.enabled = false;
        transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
        xRotation = Mathf.Clamp(pitch, -cameraVerticalClamp, cameraVerticalClamp);
        if (cam != null) cam.transform.localRotation = Quaternion.Euler(xRotation, 0, 0);
        velocity = Vector3.zero;
        if (controller != null) controller.enabled = wasEnabled;
        Physics.SyncTransforms();
        HideHint();
    }

    public void HideHint()
    {
        if (hintText != null) hintText.enabled = false;
    }

    private bool TryGetInteractionHit(out RaycastHit closest)
    {
        closest = default;
        if (rayOrigin == null) return false;
        float distance = float.PositiveInfinity;
        // A downward glance can cross the player's capsule. Keep the nearest world obstacle.
        foreach (var candidate in Physics.RaycastAll(rayOrigin.position, rayOrigin.forward, rayDistance,
                     Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
        {
            if (candidate.collider.transform.IsChildOf(transform) || candidate.distance >= distance) continue;
            if (candidate.collider.isTrigger && candidate.collider.GetComponentInParent<Interactable>() == null) continue;
            closest = candidate;
            distance = candidate.distance;
        }
        return !float.IsPositiveInfinity(distance);
    }

    public bool TryInteractInView()
    {
        if (cameraTransitionInProgress || !enabled || rayOrigin == null || MainLoop.Instance == null || MainLoop.Instance.MainLoopState != MainState.Main)
            return false;
        if (!TryGetInteractionHit(out var hit)) return false;
        var target = hit.collider.GetComponentInParent<Interactable>();
        if (target == null || !target.IsInteractionAllowed) return false;
        MainLoop.Instance.SetMainLoopState(MainState.Interact);
        target.Interact();
        return true;
    }
}
