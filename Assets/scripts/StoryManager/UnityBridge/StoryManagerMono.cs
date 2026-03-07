using UnityEngine;

public class StoryManagerMono : MonoBehaviour
{
    public static StoryManagerMono Instance;

    public StorySystem System { get; private set; }

    void Awake()
    {
        Instance = this;

        var state = new StoryState();
        System = new StorySystem(state);
    }
}
