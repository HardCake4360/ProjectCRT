using UnityEngine;
using System.Collections;
using System.Reflection;

public class Interactable: MonoBehaviour
{
    public string HintName;
    public bool canInteract = true;

    [Header("Closeup")]
    [SerializeField] private bool useCloseupCamera;
    [SerializeField] private MonoBehaviour closeupCameraBehaviour;
    [SerializeField] private int closeupPriority = 100;

    private bool cachedCloseupPriority;
    private object originalPriorityValue;
    private Camera cachedMainCamera;
    private Vector3 cachedMainCameraPosition;
    private Quaternion cachedMainCameraRotation;
    private bool hasCachedMainCameraPose;

    public virtual void Interact() { }

    protected virtual void Awake()
    {
        if (!useCloseupCamera || closeupCameraBehaviour == null)
        {
            return;
        }

        CacheOriginalPriorityIfNeeded();
        closeupCameraBehaviour.gameObject.SetActive(false);
    }
    
    public void SetInteractable(bool value)
    {
        canInteract = value;
    }

    public void SetInteractableWithDelay(float delay)
    {
        StartCoroutine(ReenableInteraction(delay));
    }

    private IEnumerator ReenableInteraction(float delay)
    {
        canInteract = false;
        yield return new WaitForSeconds(delay);
        canInteract = true;
    }

    public void EnterCloseup()
    {
        if (!useCloseupCamera || closeupCameraBehaviour == null)
        {
            return;
        }

        CacheMainCameraPose();
        CacheOriginalPriorityIfNeeded();
        closeupCameraBehaviour.gameObject.SetActive(true);
        SetCameraPriority(closeupPriority);
    }

    public void ExitCloseup()
    {
        if (!useCloseupCamera || closeupCameraBehaviour == null)
        {
            return;
        }

        RestoreCameraPriority();
        closeupCameraBehaviour.gameObject.SetActive(false);
        RestoreMainCameraPose();
    }

    private void CacheOriginalPriorityIfNeeded()
    {
        if (cachedCloseupPriority || closeupCameraBehaviour == null)
        {
            return;
        }

        PropertyInfo priorityProperty = closeupCameraBehaviour.GetType().GetProperty("Priority", BindingFlags.Instance | BindingFlags.Public);
        if (priorityProperty != null && priorityProperty.CanRead)
        {
            originalPriorityValue = priorityProperty.GetValue(closeupCameraBehaviour);
            cachedCloseupPriority = true;
        }
    }

    private void SetCameraPriority(int priority)
    {
        if (closeupCameraBehaviour == null)
        {
            return;
        }

        PropertyInfo priorityProperty = closeupCameraBehaviour.GetType().GetProperty("Priority", BindingFlags.Instance | BindingFlags.Public);
        if (priorityProperty == null || !priorityProperty.CanWrite)
        {
            return;
        }

        if (priorityProperty.PropertyType == typeof(int))
        {
            priorityProperty.SetValue(closeupCameraBehaviour, priority);
        }
    }

    private void RestoreCameraPriority()
    {
        if (!cachedCloseupPriority || closeupCameraBehaviour == null || originalPriorityValue == null)
        {
            return;
        }

        PropertyInfo priorityProperty = closeupCameraBehaviour.GetType().GetProperty("Priority", BindingFlags.Instance | BindingFlags.Public);
        if (priorityProperty != null && priorityProperty.CanWrite && priorityProperty.PropertyType == originalPriorityValue.GetType())
        {
            priorityProperty.SetValue(closeupCameraBehaviour, originalPriorityValue);
        }
    }

    private void CacheMainCameraPose()
    {
        cachedMainCamera = Camera.main;
        if (cachedMainCamera == null)
        {
            hasCachedMainCameraPose = false;
            return;
        }

        cachedMainCameraPosition = cachedMainCamera.transform.position;
        cachedMainCameraRotation = cachedMainCamera.transform.rotation;
        hasCachedMainCameraPose = true;
    }

    private void RestoreMainCameraPose()
    {
        if (!hasCachedMainCameraPose || cachedMainCamera == null)
        {
            return;
        }

        cachedMainCamera.transform.SetPositionAndRotation(cachedMainCameraPosition, cachedMainCameraRotation);
        hasCachedMainCameraPose = false;
    }
}
