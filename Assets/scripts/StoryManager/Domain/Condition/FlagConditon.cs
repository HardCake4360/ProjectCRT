public class FlagCondition : ICondition
{
    private readonly string flag;

    public FlagCondition(string flag)
    {
        this.flag = flag;
    }

    public bool Evaluate(StoryState state)
        => state.HasFlag(flag);
}
