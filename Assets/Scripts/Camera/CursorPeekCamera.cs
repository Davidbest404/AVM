using UnityEngine;

/// <summary>
/// Камера плавно подглядывает за курсором (S-кривая с пиком в середине диапазона).
/// НЕ двигает position — только localRotation.
/// Хранит базовую ориентацию как Quaternion напрямую — без round-trip через Euler.
/// Peek-углы хранятся как дельты от базы (маленькие числа около нуля).
/// </summary>
public class CursorPeekCamera : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Камера. Если пусто — Camera.main.")]
    [SerializeField] private Camera _camera;

    [Header("Rotation Bounds (degrees)")]
    [SerializeField] private float _maxYaw = 7f;
    [SerializeField] private float _maxPitch = 4f;

    [Header("Smoothing")]
    [SerializeField] private float _smoothTime = 0.4f;

    [Header("Dead Zone")]
    [Range(0f, 0.5f)]
    [SerializeField] private float _deadZone = 0.1f;

    [Header("Response Curve")]
    [Tooltip("1 = линейно, 2 = выраженный пик в середине, 3+ = резкий пик")]
    [Range(1f, 4f)]
    [SerializeField] private float _curvePower = 2f;

    [Header("Debug")]
    [Tooltip("Логировать смену base rotation (для отладки).")]
    [SerializeField] private bool _logBaseChanges = false;

    // ---- Собственное состояние ----
    // Базовая ориентация камеры в LOCAL пространстве её родителя.
    private Quaternion _baseRotation = Quaternion.identity;

    // Текущие peek-дельты в градусах (маленькие, около 0).
    private float _peekYaw;
    private float _peekPitch;

    private float _yawVelocity;
    private float _pitchVelocity;
    private bool _isEnabled = true;

    private Camera Cam => _camera != null ? _camera : Camera.main;

    // ----------------------------------------------------------------
    // Инициализация
    // ----------------------------------------------------------------

    private void Awake()
    {
        _baseRotation = transform.localRotation;
    }

    // ----------------------------------------------------------------
    // Внешний API (для OperatorPositionController)
    // ----------------------------------------------------------------

    /// <summary>Включить/выключить подглядывание (на время перелёта).</summary>
    public void SetEnabled(bool value)
    {
        _isEnabled = value;
        _yawVelocity = 0f;
        _pitchVelocity = 0f;
    }

    /// <summary>
    /// Зафиксировать новую базовую ориентацию (после прибытия камеры).
    /// worldRotation — тот угол, куда камера должна смотреть БЕЗ подглядывания.
    /// </summary>
    public void SetBaseRotation(Quaternion worldRotation)
    {
        Quaternion local = transform.parent != null
            ? Quaternion.Inverse(transform.parent.rotation) * worldRotation
            : worldRotation;

        _baseRotation = local;

        // Сбрасываем peek-дельты — камера «прилипает» к базе
        _peekYaw = 0f;
        _peekPitch = 0f;
        _yawVelocity = 0f;
        _pitchVelocity = 0f;

        ApplyRotation();

        if (_logBaseChanges)
        {
            Debug.Log($"[CursorPeek] SetBaseRotation → local={local.eulerAngles}, " +
                      $"world={worldRotation.eulerAngles}");
        }
    }

    // ----------------------------------------------------------------
    // Основной цикл
    // ----------------------------------------------------------------

    private void LateUpdate()
    {
        if (!_isEnabled || Cam == null) return;

        // Пауза (ESC-меню): peek не должен реагировать на мышь
        if (GameManager.Instance != null && GameManager.Instance.IsPaused) return;

        // 1. Позиция курсора → [-1, 1]
        Vector2 mouse = Input.mousePosition;
        Vector2 norm = new Vector2(
            (mouse.x / Screen.width) * 2f - 1f,
            (mouse.y / Screen.height) * 2f - 1f
        );

        norm.x = ApplyDeadZone(norm.x);
        norm.y = ApplyDeadZone(norm.y);

        float yawFrac = PeekCurve(Mathf.Abs(norm.x)) * Mathf.Sign(norm.x);
        float pitchFrac = PeekCurve(Mathf.Abs(norm.y)) * Mathf.Sign(norm.y);

        // Целевые ДЕЛЬТЫ (не абсолютные углы!)
        float targetPeekYaw = yawFrac * _maxYaw;
        float targetPeekPitch = -pitchFrac * _maxPitch;

        _peekYaw = Mathf.SmoothDampAngle(
            _peekYaw, targetPeekYaw, ref _yawVelocity, _smoothTime);
        _peekPitch = Mathf.SmoothDampAngle(
            _peekPitch, targetPeekPitch, ref _pitchVelocity, _smoothTime);

        _peekYaw = Mathf.Clamp(_peekYaw, -_maxYaw, _maxYaw);
        _peekPitch = Mathf.Clamp(_peekPitch, -_maxPitch, _maxPitch);

        ApplyRotation();
    }

    private void ApplyRotation()
    {
        // Дельта — чистая, локальная, около нуля. Никакого round-trip.
        Quaternion delta = Quaternion.Euler(_peekPitch, _peekYaw, 0f);
        transform.localRotation = _baseRotation * delta;
    }

    // ----------------------------------------------------------------
    // Помощники
    // ----------------------------------------------------------------

    private float PeekCurve(float t)
    {
        t = Mathf.Clamp01(t);
        if (Mathf.Approximately(_curvePower, 1f)) return t;

        float tp = Mathf.Pow(t, _curvePower);
        float tp1 = Mathf.Pow(1f - t, _curvePower);
        return tp / (tp + tp1);
    }

    private float ApplyDeadZone(float v)
    {
        float a = Mathf.Abs(v);
        if (a < _deadZone) return 0f;
        return Mathf.Sign(v) * (a - _deadZone) / (1f - _deadZone);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(transform.position, transform.forward * 3f);
    }
}