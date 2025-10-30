using UnityEngine;
using System.Collections.Generic;


public class ChoicesUIControler : MonoBehaviour
{
    private ChoicesObj choices;
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
            if (child.gameObject.GetComponent<ChoiceIndicator>()) continue;
            Destroy(child.gameObject);
        }
    }

    public void InstantiateChoices()
    {
        ClearChildren(ChoiceParent);
        choiceObjs.Clear();
        foreach (var line in choices.lines)
        {
            GameObject choice = Instantiate(choicePrefab, ChoiceParent);
            var setList = choice.GetComponent<ChoicePrefab>();

            //choice 멤버변수 초기화
            setList.InitMembers(line.name, line.OnSelectDialogue);

            choiceObjs.Add(choice);
        }
    }
    public void IndicateByIdx(int i)
    {
        int idx = 0;
        foreach(var c in choiceObjs)
        {
            if(idx == i)
            {
                c.GetComponent<ChoicePrefab>().SetSelected(true);
            }
            else
            {
                c.GetComponent<ChoicePrefab>().SetSelected(false);
            }
            idx++;
        }
    }

}

