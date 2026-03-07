public class SetFlagEffect : IEffect
{
    private readonly string flag;

    public SetFlagEffect(string flag)
    {
        this.flag = flag;
    }

    public void Apply(StoryState state)
    {
        state.SetFlag(flag);
    }
}
