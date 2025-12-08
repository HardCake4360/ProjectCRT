using UnityEngine;
using System.Collections.Generic;


public class ChoicesUIControler : MonoBehaviour
{
    [SerializeField] private ChoicesObj choices;
    public GameObject choicePrefab;
    public Transform ChoiceParent;
    private List<GameObject> choiceObjs = new List<GameObject>();

    public void SetChoices(ChoicesObj c)
    {
        choices = c;
    }
    public void ClearChildren(Transform target)
    {
        foreach (Transform child in target)
        {
            if (child.gameObject.tag == "OutOfLayout") continue;
            Destroy(child.gameObject);
        }
    }

    public void InstantiateChoices()
    {
        ClearChildren(ChoiceParent);
        choiceObjs.Clear();
        int idx = 0;
        foreach (var line in choices.lines)
        {
            GameObject choice = Instantiate(choicePrefab, ChoiceParent);
            var setList = choice.GetComponent<ChoicePrefab>();

            //choice 멤버변수 초기화
            setList.InitMembers(idx, line);

            choiceObjs.Add(choice);

            idx++;
        }
    }
    public void IndicateByIdx(int i)
    {
        Debug.Log("indicating by idx: " + i);
        foreach(var c in choiceObjs)
        {
            c.GetComponent<ChoicePrefab>().SetSelected(false);
        }
        choiceObjs[i].GetComponent<ChoicePrefab>().SetSelected(true);
    }

    public void SetSelfActive(bool active)
    {
        gameObject.SetActive(active);
    }

}

