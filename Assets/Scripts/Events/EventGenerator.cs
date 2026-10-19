using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Генерирует события поломок с интервалом, зависящим от текущей фазы.
/// Выбор события — по весам (соотношение шансов).
/// С подробным логированием для отладки.
/// </summary>
public class EventGenerator : MonoBehaviour
{
    [Header("Phases")]
    [Tooltip("Фазы игры. Сортируются по startTime автоматически.")]
    [SerializeField] private List<GamePhase> _phases = new();

    [Header("Event Pool")]
    [Tooltip("Пул событий. Выбор — по weight и CanTrigger().")]
    [SerializeField] private List<FailureEvent> _eventPool = new();

    [Header("Fallback")]
    [Tooltip("Если фазы не покрывают текущее время — использовать этот интервал.")]
    [SerializeField] private float _defaultInterval = 7f;

    [Header("Debug")]
    [Tooltip("Логировать попытки триггера и пропуски.")]
    [SerializeField] private bool _logRolls = true;

    private float _nextEventTime;
    private GamePhase _currentPhase;

    private void Awake()
    {
        _phases.Sort((a, b) => a.startTime.CompareTo(b.startTime));
    }

    private void Start()
    {
        if (_eventPool.Count == 0)
        {
            Debug.LogError("[EventGenerator] _eventPool ПУСТОЙ — добавь ассеты в инспекторе!");
        }

        if (_phases.Count == 0)
        {
            Debug.LogWarning($"[EventGenerator] _phases пустой, интервал = {_defaultInterval} сек");
        }

        _currentPhase = ResolvePhase(0f);

        if (_currentPhase != null)
            Debug.Log($"[EventGenerator] Стартовая фаза: '{_currentPhase.phaseName}', интервал {_currentPhase.eventInterval} сек");
        else
            Debug.LogWarning($"[EventGenerator] Не найдена фаза для t=0, использую default интервал {_defaultInterval} сек");

        ScheduleNext();
        Debug.Log($"[EventGenerator] Первое событие через {_nextEventTime:F1} сек");
    }

    private void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.IsGameOver) return;
        if (GameManager.Instance.IsPaused) return;

        float now = GameManager.Instance.CurrentTime;
        if (now >= _nextEventTime)
        {
            TriggerRandomEvent();
            _currentPhase = ResolvePhase(now);
            ScheduleNext();

            if (_currentPhase != null)
                Debug.Log($"[EventGenerator] Следующее событие через {_currentPhase.eventInterval} сек (фаза '{_currentPhase.phaseName}')");
        }
    }

    private GamePhase ResolvePhase(float t)
    {
        foreach (var phase in _phases)
            if (phase.Contains(t)) return phase;
        return _phases.Count > 0 ? _phases[_phases.Count - 1] : null;
    }

    private void ScheduleNext()
    {
        float interval = _currentPhase != null ? _currentPhase.eventInterval : _defaultInterval;
        _nextEventTime = GameManager.Instance.CurrentTime + interval;
    }

    private void TriggerRandomEvent()
    {
        if (_eventPool.Count == 0) return;

        var available = new List<FailureEvent>();
        foreach (var ev in _eventPool)
        {
            if (ev == null)
            {
                if (_logRolls) Debug.LogWarning("[EventGenerator] Пустая ссылка в _eventPool");
                continue;
            }

            bool can = ev.CanTrigger();
            if (can) available.Add(ev);
            else if (_logRolls)
                Debug.Log($"[EventGenerator] Пропущено '{ev.displayName}' — CanTrigger = false");
        }

        if (available.Count == 0)
        {
            if (_logRolls)
                Debug.LogWarning("[EventGenerator] Все события отфильтрованы — ни одно не сработает. " +
                                 "Проверь targetSystemName, регистрацию в GameManager, min/max прогресс.");
            return;
        }

        int totalWeight = 0;
        foreach (var ev in available) totalWeight += ev.weight;

        int roll = Random.Range(0, totalWeight);
        int acc = 0;
        foreach (var ev in available)
        {
            acc += ev.weight;
            if (roll < acc)
            {
                if (_logRolls)
                    Debug.Log($"[EventGenerator] ТРИГГЕР: '{ev.displayName}' " +
                              $"(вес {ev.weight}/{totalWeight}, доступно {available.Count} событий)");
                ev.Trigger();
                return;
            }
        }
    }
}