using System;
using UnityEngine;

/// <summary>
/// Система синхронизации: два параметра волны (частота = длина, амплитуда = высота).
/// Параметры меняются ТОЛЬКО через TriggerFailure() (событие) и ввод шара.
/// Никакого автоматического дрейфа.
/// </summary>
public class OscilloscopeSystem : MonoBehaviour, IFailableSystem
{
    public string SystemName => "Sync";
    public bool IsBroken => Quality < _brokenQualityThreshold;

    [Header("Target (base) parameters")]
    [Tooltip("Целевая частота — куда нужно вернуть волну.")]
    [SerializeField] private float _baseFrequency = 4f;
    [Tooltip("Целевая амплитуда — куда нужно вернуть волну.")]
    [SerializeField] private float _baseAmplitude = 0.25f;

    [Header("Allowed range")]
    [SerializeField] private float _minFrequency = 1f;
    [SerializeField] private float _maxFrequency = 8f;
    [SerializeField] private float _minAmplitude = 0.05f;
    [SerializeField] private float _maxAmplitude = 0.45f;

    [Header("Tolerance (dead zone — no penalty)")]
    [SerializeField] private float _frequencyTolerance = 0.6f;
    [SerializeField] private float _amplitudeTolerance = 0.07f;

    [Header("Ball input")]
    [SerializeField] private TrackballMouse _trackball;
    [SerializeField] private float _frequencyPerDegree = 0.012f;
    [SerializeField] private float _amplitudePerDegree = 0.003f;

    [Header("Debuff")]
    [Range(0.05f, 1f)]
    [SerializeField] private float _worstModifier = 0.3f;
    [Range(0f, 1f)]
    [SerializeField] private float _brokenQualityThreshold = 0.4f;

    [Header("Failure")]
    [Tooltip("Максимальное отклонение частоты при сбое.")]
    [SerializeField] private float _failureFrequencyJolt = 1.5f;
    [Tooltip("Максимальное отклонение амплитуды при сбое.")]
    [SerializeField] private float _failureAmplitudeJolt = 0.2f;

    // ---- Публичное ----
    public float CurrentFrequency => _currentFrequency;
    public float CurrentAmplitude => _currentAmplitude;
    public float BaseFrequency => _baseFrequency;
    public float BaseAmplitude => _baseAmplitude;
    public float Quality { get; private set; } = 1f;

    public event Action<SystemState> OnStateChanged;
    public event Action OnParametersChanged;

    // ---- Внутреннее ----
    private float _currentFrequency;
    private float _currentAmplitude;
    private SystemState _currentState = SystemState.Normal;

    public void Initialize()
    {
        _currentFrequency = _baseFrequency;
        _currentAmplitude = _baseAmplitude;
        Quality = 1f;
        _currentState = SystemState.Normal;
        NotifyChanged();
    }

    public float GetPerformanceModifier()
    {
        return Mathf.Lerp(_worstModifier, 1f, Quality);
    }

    // ---- IFailableSystem ----

    public void TriggerFailure()
    {
        // Резкий рывок в случайную сторону
        _currentFrequency += UnityEngine.Random.Range(-_failureFrequencyJolt, _failureFrequencyJolt);
        _currentAmplitude += UnityEngine.Random.Range(-_failureAmplitudeJolt, _failureAmplitudeJolt);
        ClampParams();
        RecalculateQuality();
        NotifyChanged();
        Debug.Log($"[Sync] Сбой: f={_currentFrequency:F2}, a={_currentAmplitude:F2}, Q={Quality:F2}");
    }

    // ---- Update: только ввод от шара ----

    private void Update()
    {
        if (_trackball == null) return;

        Vector2 delta = _trackball.FrameDelta;
        if (delta.sqrMagnitude < 0.001f) return;

        _currentFrequency += delta.x * _frequencyPerDegree;
        _currentAmplitude += delta.y * _amplitudePerDegree;
        ClampParams();
        RecalculateQuality();
        NotifyChanged();
    }

    // ---- Внутреннее ----

    private void ClampParams()
    {
        _currentFrequency = Mathf.Clamp(_currentFrequency, _minFrequency, _maxFrequency);
        _currentAmplitude = Mathf.Clamp(_currentAmplitude, _minAmplitude, _maxAmplitude);
    }

    private void RecalculateQuality()
    {
        float freqError = Mathf.Abs(_currentFrequency - _baseFrequency);
        float ampError = Mathf.Abs(_currentAmplitude - _baseAmplitude);

        float freqOver = Mathf.Max(0f, freqError - _frequencyTolerance);
        float ampOver = Mathf.Max(0f, ampError - _amplitudeTolerance);

        float maxFreqError = Mathf.Max(0.001f, _maxFrequency - _baseFrequency - _frequencyTolerance);
        float maxAmpError = Mathf.Max(0.001f, _maxAmplitude - _baseAmplitude - _amplitudeTolerance);

        float freqPenalty = Mathf.Clamp01(freqOver / maxFreqError);
        float ampPenalty = Mathf.Clamp01(ampOver / maxAmpError);

        float worst = Mathf.Max(freqPenalty, ampPenalty);
        Quality = 1f - worst;

        SystemState newState;
        if (Quality < _brokenQualityThreshold) newState = SystemState.Broken;
        else if (worst > 0.01f) newState = SystemState.Warning;
        else newState = SystemState.Normal;

        if (newState != _currentState)
        {
            _currentState = newState;
            OnStateChanged?.Invoke(newState);
        }
    }

    private void NotifyChanged()
    {
        OnParametersChanged?.Invoke();
    }
}