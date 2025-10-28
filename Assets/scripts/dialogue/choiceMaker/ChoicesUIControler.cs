using UnityEngine;
using System.Collections.Generic;


public class ChoicesUIControler : MonoBehaviour
{
    private ChoicesObj choices;
    public GameObject choicePrefab;
    public Transform ChoiceParent;
    public ChoiceIndicator Indicator;
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
        Indicator.SetTargets(choiceObjs);
        Indicator.IndicateByIdx(0);
    }
}
