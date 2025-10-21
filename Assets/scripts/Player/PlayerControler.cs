using UnityEngine;

public class PlayerControler : MonoBehaviour
{
    CharacterController charCon;
    [SerializeField] GameObject cam;
    [SerializeField] float speed;
    [SerializeField] float sensitivity;
    [SerializeField] float cameraVerticalClamp;
    
    private float horizontalInput;
    private float verticalInput;

    private float xRotation;//수직회전
    
    private Vector3 velocity;


    private void Start()
    {
        charCon = GetComponent<CharacterController>();
        horizontalInput = 0f;
        verticalInput   = 0f;
        Cursor.lockState = CursorLockMode.Locked;
    }

    private void Update()
    {
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
    }

    public Vector3 GetMoveVector()
    {
        Vector3 vel = (transform.forward * verticalInput) + (transform.right * horizontalInput);
        return new Vector3(vel.x, -9.8f, vel.z) * speed;
    }
}
