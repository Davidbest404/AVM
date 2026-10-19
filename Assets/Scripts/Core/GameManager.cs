using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Центральный менеджер игры. Управляет таймером, прогрессом и списком систем.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Core Settings")]
    [SerializeField] private float _totalGameTime = 300f;

    [Tooltip("+1% каждые 2 сек = 0.5% / сек")]
    [SerializeField] private float _baseProgressPerSecond = 0.5f;

    [Header("Systems")]
    [SerializeField] private List<MonoBehaviour> _systemComponents = new();

    public float CurrentTime { get; private set; }
    public float Progress { get; private set; }
    public bool IsGameOver { get; private set; }
    public bool IsPaused { get; private set; }

    public event Action<float> OnProgressChanged;
    public event Action<float> OnTimeChanged;
    public event Action OnGameWin;
    public event Action OnGameLose;

    private readonly List<IMachineSystem> _systems = new();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    private void Start()
    {
        foreach (var comp in _systemComponents)
        {
            if (comp is IMachineSystem sys)
            {
                _systems.Add(sys);
                sys.Initialize();
            }
            else if (comp != null)
            {
                Debug.LogWarning($"[GameManager] {comp.name} не реализует IMachineSystem");
            }
        }

        CurrentTime = 0f;
        Progress = 0f;
        OnProgressChanged?.Invoke(Progress);
        OnTimeChanged?.Invoke(CurrentTime);
    }

    private void Update()
    {
        if (IsGameOver || IsPaused) return;

        CurrentTime += Time.deltaTime;
        OnTimeChanged?.Invoke(CurrentTime);

        float totalModifier = 1f;
        foreach (var sys in _systems)
            totalModifier *= sys.GetPerformanceModifier();

        Progress += _baseProgressPerSecond * totalModifier * Time.deltaTime;
        Progress = Mathf.Clamp(Progress, 0f, 100f);
        OnProgressChanged?.Invoke(Progress);

        if (Progress >= 100f) { Win(); return; }
        if (CurrentTime >= _totalGameTime) { Lose(); return; }
    }

    public T GetSystem<T>(string systemName = null) where T : class, IMachineSystem
    {
        foreach (var s in _systems)
        {
            if (s is not T t) continue;
            if (string.IsNullOrEmpty(systemName) || s.SystemName == systemName)
                return t;
        }
        return null;
    }

    /// <summary>Проверяет, блокирует ли какая-либо система ввод прямо сейчас.</summary>
    public bool IsInputBlocked()
    {
        foreach (var s in _systems)
            if (s is IInputBlockingSystem blocker && blocker.IsBlockingInput())
                return true;
        return false;
    }

    public void SetPaused(bool value) => IsPaused = value;

    private void Win() { IsGameOver = true; Debug.Log("[GameManager] ПОБЕДА."); OnGameWin?.Invoke(); }
    private void Lose() { IsGameOver = true; Debug.Log("[GameManager] ПОРАЖЕНИЕ."); OnGameLose?.Invoke(); }
}