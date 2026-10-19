using System;
using UnityEngine;

/// <summary>
/// Система ленты. N — степень зажевывания (0 = работает, Max = зажёвана).
/// При поломке N растёт 0→Max, колесо замедляется. При починке (кнопка держится)
/// N уменьшается, колесо крутится назад. Колесо стоит только при N=Max.
/// </summary>
public class TapeSystem : MonoBehaviour, IFailableSystem
{
    public string SystemName => "Tape";
    public bool IsBroken => _state == TapeState.Jammed && _currentJamValue >= _maxJamValue;
    public TapeState State => _state;
    public bool IsRewinding => _isRewinding;

    /// <summary>Текущая угловая скорость колеса (град/сек). Положительная — вперёд, отрицательная — назад, 0 — стоит.</summary>
    public float CurrentSpinSpeed
    {
        get
        {
            if (_state == TapeState.Working)
                return _isRewinding ? _rewindSpinSpeed : _workSpinSpeed;
            if (_state == TapeState.Jammed)
                return _isRewinding ? _rewindSpinSpeed : 0f;
            return 0f;
        }
    }

    [Header("Debuff")]
    [Tooltip("Множитель скорости при полном зажевывании (N = Max).")]
    [Range(0f, 1f)]
    [SerializeField] private float _jammedModifier = 0.5f;

    [Header("Jam Value")]
    [Tooltip("Максимальная степень зажевывания (N = Max).")]
    [SerializeField] private float _maxJamValue = 3f;

    [Tooltip("Скорость роста N при зажевывании (ед/сек). При 1 и Max=3: полное зажевывание за 3 сек.")]
    [SerializeField] private float _jamGrowthSpeed = 1f;

    [Tooltip("Скорость уменьшения N при удержании кнопки (ед/сек).")]
    [SerializeField] private float _drainSpeed = 1.5f;

    [Header("Wheel")]
    [Tooltip("Единственное колесо, крутящее ленту.")]
    [SerializeField] private Transform _wheel;

    [Tooltip("Скорость при нормальной работе.")]
    [SerializeField] private float _workSpinSpeed = 90f;

    [Tooltip("Скорость при перемотке (отрицательная — назад).")]
    [SerializeField] private float _rewindSpinSpeed = -360f;

    public event Action<SystemState> OnStateChanged;

    public float MaxJamValue => _maxJamValue;
    public float CurrentJamValue => _currentJamValue;

    private TapeState _state = TapeState.Working;
    private bool _isRewinding;
    private float _currentJamValue;

    public void Initialize()
    {
        _state = TapeState.Working;
        _isRewinding = false;
        _currentJamValue = 0f;
        NotifyStateChanged();
    }

    public float GetPerformanceModifier()
    {
        if (_state == TapeState.Working) return 1f;
        // Плавная деградация по мере роста N
        float t = Mathf.Clamp01(_currentJamValue / Mathf.Max(0.001f, _maxJamValue));
        return Mathf.Lerp(1f, _jammedModifier, t);
    }

    // ---- IFailableSystem ----

    public void TriggerFailure()
    {
        if (_state == TapeState.Jammed) return;
        _state = TapeState.Jammed;
        _currentJamValue = 0f; // НЕ Max — растёт постепенно
        NotifyStateChanged();
        Debug.Log("[TapeSystem] Началось зажевывание (N=0)");
    }

    // ---- Внешний API ----

    public void SetRewinding(bool value)
    {
        _isRewinding = value;
    }

    public void CompleteRewind()
    {
        if (_state != TapeState.Jammed) return;
        _state = TapeState.Working;
        _isRewinding = false;
        _currentJamValue = 0f;
        NotifyStateChanged();
        Debug.Log("[TapeSystem] Перемотка завершена — лента работает");
    }

    // ---- Внутреннее ----

    private void NotifyStateChanged()
    {
        var sys = _state == TapeState.Working ? SystemState.Normal : SystemState.Warning;
        OnStateChanged?.Invoke(sys);
    }

    private void Update()
    {
        if (_state == TapeState.Working)
        {
            float speed = _isRewinding ? _rewindSpinSpeed : _workSpinSpeed;
            RotateWheel(speed);
            return;
        }

        // Jammed
        if (_isRewinding)
        {
            // Игрок держит — N уменьшается
            _currentJamValue -= _drainSpeed * Time.deltaTime;
            if (_currentJamValue <= 0f)
            {
                _currentJamValue = 0f;
                CompleteRewind();
                return;
            }
            RotateWheel(_rewindSpinSpeed);
        }
        else
        {
            // N растёт до Max
            _currentJamValue = Mathf.Min(
                _maxJamValue,
                _currentJamValue + _jamGrowthSpeed * Time.deltaTime);

            // Колесо замедляется, при N=Max стоит
            float slowFactor = 1f - (_currentJamValue / Mathf.Max(0.001f, _maxJamValue));
            RotateWheel(_workSpinSpeed * slowFactor);
        }
    }

    private void RotateWheel(float speed)
    {
        if (_wheel != null) _wheel.Rotate(0f, 0f, speed * Time.deltaTime);
    }
}