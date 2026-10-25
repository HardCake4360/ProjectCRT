/// <summary>Optional closeup behaviour that owns its entry and exit timing.</summary>
public interface ICloseupCameraTransition
{
    bool IsEntering { get; }
    void BeginCloseup();
    void EndCloseup();
}
