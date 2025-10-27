using UnityEngine;
using System.Collections.Generic;


public class ChoicesUIControler : MonoBehaviour
{
    private ChoicesObj choices;
    public GameObject choicePrefab;
    private List<GameObject> choiceObjs;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void InstantiateChoices()
    {
        foreach (var line in choices.lines)
        {
            GameObject choice = Instantiate(choicePrefab);
            var setList = choice.GetComponent<ChoicePrefab>();

            //choice 멤버변수 초기화
            setList.InitMembers(line.name, line.OnSelectDialogue);

            choiceObjs.Add(choice);
        }
    }
}
