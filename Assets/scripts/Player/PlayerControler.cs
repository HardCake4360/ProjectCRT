using UnityEngine;
using TMPro;

public class PlayerControler : MonoBehaviour
{
    public static PlayerControler Instance { get; private set; }

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
    [SerializeField] private bool isInteracting;
    public bool IsInteracting() { return isInteracting; }
    public void SetInteract(bool val) 
    { 
        isInteracting = val;
        Debug.Log("플레이어 상호작용: " + val);
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        charCon = GetComponent<CharacterController>();
        horizontalInput = 0f;
        verticalInput   = 0f;
        Cursor.lockState = CursorLockMode.Locked;
    }

    private void Update()
    {
        if (isInteracting) return;//상호작용중에는 모든 행동 정지

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
        if (Physics.Raycast(rayOrigin.position, rayOrigin.forward, out hitInfo, rayDistance))
        {
            // 디버깅 용으로 레이를 시각화
            Debug.DrawRay(rayOrigin.position, rayOrigin.forward * rayDistance, Color.green);
            
            //Interactable 인터페이스 검색
            Interactable interactableObject = hitInfo.collider.GetComponent<Interactable>();
            if (interactableObject)
            {
                hintText.enabled = true;
                hintText.text = interactableObject.HintName;
            }

            if (InputManager.Instance.IsAnyKeyPressedIn(InputManager.Instance.interactionKeys))
            {
                if (interactableObject != null && interactableObject.canInteract)
                {
                    isInteracting = true;
                    interactableObject.Interact();
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
            hintText.enabled = false;
        }

    }

    public Vector3 GetMoveVector()
    {
        Vector3 vel = (transform.forward * verticalInput) + (transform.right * horizontalInput);
        return new Vector3(vel.x, -9.8f, vel.z) * speed;
    }
}
