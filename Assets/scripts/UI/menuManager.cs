using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class menuManager : MonoBehaviour
{
    public bool isUIActive;
    [SerializeField] private float transSpeed;
    [SerializeField] HiddenUI[] UIs;

    public void ToggleByIdx(int i,bool show)
    {
        StartCoroutine(UIs[i].MoveRoutine(show));
    }
}
