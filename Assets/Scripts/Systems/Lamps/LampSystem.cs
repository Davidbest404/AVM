using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Система ламп АВМ. Управляет 6 сокетами, реализует IFailableSystem
/// и IInputBlockingSystem. При старте автоматически заполняет пустые сокеты лампами.
/// </summary>
public class LampSystem : MonoBehaviour, IFailableSystem, IInputBlockingSystem
{
    public static LampSystem Instance { get; private set; }

    public string SystemName => "Lamps";
    public bool IsBroken => CountWorking() == 0;

    /// <summary>Сколько всего сокетов (лампомест).</summary>
    public int TotalLampsCount => _sockets != null ? _sockets.Count : 0;

    /// <summary>Сколько ламп сейчас в состоянии Working.</summary>
    public int WorkingLampsCount => CountWorking();

    /// <summary>Сколько ламп не Working (Damaged / Faulty / нет вовсе).</summary>
    public int BrokenLampsCount => TotalLampsCount - WorkingLampsCount;

    [Header("Sockets")]
    [Tooltip("6 сокетов. Перетащить в инспекторе.")]
    [SerializeField] private List<LampSocket> _sockets = new();

    [Header("References")]
    [SerializeField] private LampInteractionController _controller;

    [Header("Startup Fill")]
    [Tooltip("Автоматически заполнять пустые сокеты при старте.")]
    [SerializeField] private bool _fillEmptySocketsOnStart = true;

    [Tooltip("Префаб лампы. Обязателен для автозаполнения.")]
    [SerializeField] private Lamp _lampPrefab;

    public event Action<SystemState> OnStateChanged;

    private SystemState _currentState = SystemState.Normal;
    private float _recalcTimer;

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    public void Initialize()
    {
        // ---- АВТОЗАПОЛНЕНИЕ ----
        if (_fillEmptySocketsOnStart)
        {
            if (_lampPrefab == null)
            {
                Debug.LogError("[LampSystem] _fillEmptySocketsOnStart = true, но _lampPrefab не назначен!");
            }
            else
            {
                int created = 0;
                foreach (var s in _sockets)
                {
                    if (s == null) continue;
                    if (s.IsEmpty)
                    {
                        Lamp newLamp = Instantiate(
                            _lampPrefab,
                            s.InstallPoint.position,
                            s.InstallPoint.rotation);
                        s.SetLamp(newLamp);
                        newLamp.InstallTo(s);
                        created++;
                    }
                }
                Debug.Log($"[LampSystem] Автозаполнение: создано {created} ламп.");
            }
        }

        // Дозакрепляем лампы, если они были в сцене
        foreach (var s in _sockets)
        {
            if (s == null) continue;
            if (s.CurrentLamp != null && s.CurrentLamp.Socket == null)
                s.CurrentLamp.InstallTo(s);
        }

        RecalculateState();

        int working = CountWorking();
        Debug.Log($"[LampSystem] Initialize: working={working}/{_sockets.Count}");
    }

    private void Update()
    {
        _recalcTimer += Time.deltaTime;
        if (_recalcTimer >= 0.5f)
        {
            _recalcTimer = 0f;
            RecalculateState();
        }
    }

    public float GetPerformanceModifier()
    {
        int working = CountWorking();
        if (working == 0) return 0f;
        return (float)working / Mathf.Max(1, _sockets.Count);
    }

    public void TriggerFailure()
    {
        var working = new List<Lamp>();
        foreach (var s in _sockets)
            if (s?.CurrentLamp != null && s.CurrentLamp.State == LampState.Working)
                working.Add(s.CurrentLamp);

        if (working.Count > 0)
        {
            working[UnityEngine.Random.Range(0, working.Count)].SetState(LampState.Damaged);
            RecalculateState();
            return;
        }

        var damaged = new List<Lamp>();
        foreach (var s in _sockets)
            if (s?.CurrentLamp != null && s.CurrentLamp.State == LampState.Damaged)
                damaged.Add(s.CurrentLamp);

        if (damaged.Count > 0)
        {
            damaged[UnityEngine.Random.Range(0, damaged.Count)].SetState(LampState.Faulty);
            RecalculateState();
        }
    }

    public bool IsBlockingInput()
    {
        return _controller != null && _controller.IsBusy;
    }

    public void NotifyLampsChanged() => RecalculateState();

    // CountWorking — уже корректно:
    private int CountWorking()
    {
        int n = 0;
        foreach (var s in _sockets)
            if (s?.CurrentLamp != null && s.CurrentLamp.State == LampState.Working)
                n++;
        return n;
    }

    private void RecalculateState()
    {
        int working = CountWorking();
        int broken = 0;
        foreach (var s in _sockets)
        {
            if (s == null) continue;
            if (s.CurrentLamp == null) broken++;
            else if (s.CurrentLamp.State != LampState.Working) broken++;
        }

        SystemState newState;
        if (working == 0) newState = SystemState.Broken;
        else if (broken > 0) newState = SystemState.Warning;
        else newState = SystemState.Normal;

        if (newState != _currentState)
        {
            _currentState = newState;
            OnStateChanged?.Invoke(newState);
        }
    }
}