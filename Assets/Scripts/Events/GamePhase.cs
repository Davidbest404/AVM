using UnityEngine;

/// <summary>
/// Фаза игры: временной отрезок + интервал между событиями.
/// Создаётся как ассет в проекте.
/// </summary>
[CreateAssetMenu(menuName = "AVM/Game Phase", fileName = "Phase_")]
public class GamePhase : ScriptableObject
{
    [Header("Info")]
    public string phaseName = "Фаза";

    [Header("Time Range (sec)")]
    [Tooltip("Начало фазы (включительно).")]
    public float startTime = 0f;

    [Tooltip("Конец фазы (не включая).")]
    public float endTime = 60f;

    [Header("Events")]
    [Tooltip("Интервал между событиями в этой фазе, сек.")]
    public float eventInterval = 7f;

    /// <summary>Попадает ли время t в эту фазу?</summary>
    public bool Contains(float t) => t >= startTime && t < endTime;
}