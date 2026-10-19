/// <summary>
/// Система, которую можно сломать через FailureEvent.
/// Наследуется от IMachineSystem, поэтому по-прежнему совместима с GameManager.
/// </summary>
public interface IFailableSystem : IMachineSystem
{
    /// <summary>Принудительно вызвать поломку этой системы.</summary>
    void TriggerFailure();
}