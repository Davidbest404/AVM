using UnityEngine;

/// <summary>
/// Абстрактное событие поломки. Наследники решают, что именно делать.
/// Вес используется при ролле в EventGenerator (соотношение шансов).
/// </summary>
public abstract class FailureEvent : ScriptableObject
{
    [Header("Info")]
    public string displayName = "Событие";

    [TextArea(2, 4)]
    public string description = "";

    [Header("Probability")]
    [Tooltip("Относительный вес. Чем больше — тем чаще выпадает.")]
    [Min(1)]
    public int weight = 10;

    [Header("Conditions")]
    [Tooltip("Минимальный прогресс (%), с которого может триггериться.")]
    [Range(0f, 100f)]
    public float minProgress = 0f;

    [Tooltip("Максимальный прогресс (%), после которого не триггерится.")]
    [Range(0f, 100f)]
    public float maxProgress = 100f;

    /// <summary>
    /// Базовые условия: игра идёт, прогресс в диапазоне.
    /// Наследники могут дополнять.
    /// </summary>
    public virtual bool CanTrigger()
    {
        if (GameManager.Instance == null) return false;
        if (GameManager.Instance.IsGameOver) return false;

        float p = GameManager.Instance.Progress;
        return p >= minProgress && p <= maxProgress;
    }

    /// <summary>Что происходит при срабатывании.</summary>
    public abstract void Trigger();
}