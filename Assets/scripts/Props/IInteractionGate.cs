/// <summary>An additional permission independent of temporary interaction cooldowns.</summary>
public interface IInteractionGate
{
    bool AllowsInteraction { get; }
}
