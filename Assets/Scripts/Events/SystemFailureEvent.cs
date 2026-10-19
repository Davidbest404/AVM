using UnityEngine;

/// <summary>
/// Событие-поломка конкретной системы по её SystemName.
/// </summary>
[CreateAssetMenu(menuName = "AVM/Failure Events/System Failure", fileName = "Event_")]
public class SystemFailureEvent : FailureEvent
{
    [Header("Target")]
    [Tooltip("SystemName целевой системы: 'Lamps', 'Tape', 'Sync', 'Cooling'.")]
    public string targetSystemName = "Lamps";

    public override bool CanTrigger()
    {
        if (!base.CanTrigger()) return false;

        if (GameManager.Instance == null)
        {
            Debug.LogWarning($"[SystemFailureEvent '{name}'] GameManager.Instance = null");
            return false;
        }

        var sys = GameManager.Instance.GetSystem<IFailableSystem>(targetSystemName);
        if (sys == null)
        {
            Debug.LogWarning($"[SystemFailureEvent '{name}'] Система '{targetSystemName}' не найдена. " +
                             $"Проверь targetSystemName и регистрацию в GameManager._systemComponents.");
            return false;
        }

        if (sys.IsBroken) return false;

        return true;
    }

    public override void Trigger()
    {
        var sys = GameManager.Instance.GetSystem<IFailableSystem>(targetSystemName);
        if (sys == null)
        {
            Debug.LogError($"[SystemFailureEvent '{name}'] Trigger: система '{targetSystemName}' не найдена.");
            return;
        }
        sys.TriggerFailure();
    }
}