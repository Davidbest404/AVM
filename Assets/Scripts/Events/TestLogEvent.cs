using UnityEngine;

/// <summary>
/// Тестовое событие: только пишет в лог. Используется до появления реальных систем.
/// </summary>
[CreateAssetMenu(menuName = "AVM/Failure Events/Test Log", fileName = "Event_TestLog")]
public class TestLogEvent : FailureEvent
{
    public override void Trigger()
    {
        Debug.Log($"[TestLogEvent] {displayName} (прогресс {GameManager.Instance.Progress:F1}%)");
    }
}