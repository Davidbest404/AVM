using System;

/// <summary>
/// Общий интерфейс всех систем АВМ (лампы, лента, синхронизация, охлаждение).
/// Позволяет GameManager работать с ними единообразно (принцип DIP).
/// </summary>
public interface IMachineSystem
{
    string SystemName { get; }
    bool IsBroken { get; }

    /// <summary>Множитель скорости прогресса: 1 = нет влияния, 0 = полная остановка.</summary>
    float GetPerformanceModifier();

    /// <summary>Вызывается один раз при старте игры.</summary>
    void Initialize();

    /// <summary>Система уведомляет о смене состояния (для визуала, звука, камеры).</summary>
    event Action<SystemState> OnStateChanged;
}