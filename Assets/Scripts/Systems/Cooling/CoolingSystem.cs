using System;
using UnityEngine;

public enum FanState
{
    Working,      // Крутится
    Jammed,       // Заклинило, таймер до перегрева
    Overheating,  // Перегрев, ждём починки
    Restarting    // Раскручивается после починки
}

/// <summary>
/// Система охлаждения. Вентилятор клинит по событию. Игрок «стучит» (кликает)
/// по вентилятору — с шансом чинит. До перегрева — рестарт 3 сек,
/// после перегрева — 5 сек.
/// </summary>
public class CoolingSystem : MonoBehaviour, IFailableSystem
{
    public string SystemName => "Cooling";
    public bool IsBroken => _state == FanState.Jammed || _state == FanState.Overheating;
    public FanState State => _state;

    [Header("Timing")]
    [Tooltip("Секунд до перегрева после клина.")]
    [SerializeField] private float _overheatTime = 12f;
    [Tooltip("Длительность рестарта если починили до перегрева.")]
    [SerializeField] private float _restartDurationNormal = 3f;
    [Tooltip("Длительность рестарта если починили после перегрева.")]
    [SerializeField] private float _restartDurationAfterOverheat = 5f;

    [Header("Performance Modifiers")]
    [Range(0f, 1f)][SerializeField] private float _jammedModifier = 0.7f;
    [Range(0f, 1f)][SerializeField] private float _overheatingModifier = 0.3f;
    [Range(0f, 1f)][SerializeField] private float _restartingModifier = 0.5f;

    [Header("Fan Visual")]
    [SerializeField] private Transform _fanBlades;
    [SerializeField] private float _workingSpinSpeed = 720f;

    [Header("Overheat Visual")]
    [SerializeField] private ParticleSystem _steamParticles;
    [SerializeField] private Light _overheatLight;
    [SerializeField] private Color _overheatLightColor = new Color(1f, 0.3f, 0.1f);
    [SerializeField] private float _overheatLightIntensity = 2f;
    [SerializeField] private float _overheatLightPulseSpeed = 8f;

    public event Action<SystemState> OnStateChanged;

    private FanState _state = FanState.Working;
    private float _timer;
    private float _currentSpin;

    public void Initialize()
    {
        _state = FanState.Working;
        _timer = 0f;
        _currentSpin = _workingSpinSpeed;
        if (_steamParticles != null) _steamParticles.Stop();
        if (_overheatLight != null) _overheatLight.enabled = false;
        Notify();
    }

    public float GetPerformanceModifier()
    {
        return _state switch
        {
            FanState.Working => 1f,
            FanState.Jammed => _jammedModifier,
            FanState.Overheating => _overheatingModifier,
            FanState.Restarting => _restartingModifier,
            _ => 1f
        };
    }

    // ---- IFailableSystem ----

    public void TriggerFailure()
    {
        if (_state == FanState.Working)
        {
            SetState(FanState.Jammed);
            _timer = 0f;
            Debug.Log("[Cooling] Вентилятор заклинило");
        }
    }

    // ---- Внешний API (для FanKnock) ----

    /// <summary>Игрок успешно «постучал» — запускаем рестарт.</summary>
    public void OnKnockSuccess()
    {
        if (_state == FanState.Jammed)
            BeginRestart(_restartDurationNormal);
        else if (_state == FanState.Overheating)
            BeginRestart(_restartDurationAfterOverheat);
    }

    // ---- Внутреннее ----

    private void BeginRestart(float duration)
    {
        SetState(FanState.Restarting);
        _timer = duration;
        if (_steamParticles != null) _steamParticles.Stop();
        if (_overheatLight != null) _overheatLight.enabled = false;
        Debug.Log($"[Cooling] Перезапуск {duration} сек");
    }

    private void SetState(FanState s)
    {
        if (_state == s) return;
        _state = s;

        if (s == FanState.Overheating)
        {
            if (_steamParticles != null) _steamParticles.Play();
            if (_overheatLight != null) _overheatLight.enabled = true;
        }
        Notify();
    }

    private void Notify()
    {
        SystemState sys = _state switch
        {
            FanState.Working => SystemState.Normal,
            FanState.Jammed => SystemState.Warning,
            FanState.Overheating => SystemState.Broken,
            FanState.Restarting => SystemState.Repairing,
            _ => SystemState.Normal
        };
        OnStateChanged?.Invoke(sys);
    }

    private void Update()
    {
        switch (_state)
        {
            case FanState.Working:
                _currentSpin = Mathf.Lerp(_currentSpin, _workingSpinSpeed, Time.deltaTime * 3f);
                break;

            case FanState.Jammed:
                _timer += Time.deltaTime;
                _currentSpin = Mathf.Lerp(_currentSpin, 0f, Time.deltaTime * 2f);
                if (_timer >= _overheatTime)
                {
                    SetState(FanState.Overheating);
                    Debug.Log("[Cooling] ПЕРЕГРЕВ");
                }
                break;

            case FanState.Overheating:
                _currentSpin = Mathf.Lerp(_currentSpin, 0f, Time.deltaTime * 2f);
                break;

            case FanState.Restarting:
                _timer -= Time.deltaTime;
                _currentSpin = Mathf.Lerp(_currentSpin, _workingSpinSpeed, Time.deltaTime * 1.5f);
                if (_timer <= 0f)
                {
                    SetState(FanState.Working);
                    Debug.Log("[Cooling] Рестарт завершён");
                }
                break;
        }

        if (_fanBlades != null)
            _fanBlades.Rotate(0f, 0f, _currentSpin * Time.deltaTime);

        if (_overheatLight != null && _overheatLight.enabled)
        {
            float pulse = 0.7f + 0.3f * Mathf.Sin(Time.time * _overheatLightPulseSpeed);
            _overheatLight.color = _overheatLightColor;
            _overheatLight.intensity = _overheatLightIntensity * pulse;
        }
    }
}