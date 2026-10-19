using UnityEngine;

/// <summary>
/// «Постучать» по вентилятору — быстрые клики с растущим шансом починить.
/// Серия прерывается, если игрок медлит.
/// </summary>
[RequireComponent(typeof(Collider))]
public class FanKnock : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CoolingSystem _coolingSystem;
    [SerializeField] private OperatorInputRouter _inputRouter;
    [SerializeField] private Camera _camera;

    [Header("Knock Chances")]
    [Range(0f, 1f)][SerializeField] private float _baseChance = 0.10f;
    [Range(0f, 0.5f)][SerializeField] private float _increasePerStreak = 0.05f;
    [Range(0f, 1f)][SerializeField] private float _maxChance = 0.60f;

    [Tooltip("Максимальный интервал между кликами для серии (сек).")]
    [SerializeField] private float _comboWindow = 0.8f;

    [Tooltip("Через сколько секунд без кликов серия сбрасывается.")]
    [SerializeField] private float _streakResetTime = 1.2f;

    [Header("Feel")]
    [SerializeField] private AudioSource _knockSound;
    [SerializeField] private Transform _fanVisual;
    [SerializeField] private float _shakeAmount = 0.01f;
    [SerializeField] private float _shakeDuration = 0.1f;

    [Header("Debug")]
    [SerializeField] private bool _debugLog = false;

    private int _hitStreak;
    private float _lastClickTime = -99f;
    private float _lastClickRealTime = -99f;
    private Vector3 _fanRestPos;
    private float _shakeTimer;
    private Collider _collider;

    private Camera Cam => _camera != null ? _camera : Camera.main;

    private void Awake()
    {
        _collider = GetComponent<Collider>();
        if (_fanVisual != null) _fanRestPos = _fanVisual.localPosition;
    }

    private void Update()
    {
        // Шейк
        if (_fanVisual != null && _shakeTimer > 0f)
        {
            _shakeTimer -= Time.deltaTime;
            float n = Mathf.Clamp01(_shakeTimer / _shakeDuration);
            Vector3 jitter = Random.insideUnitSphere * _shakeAmount * n;
            _fanVisual.localPosition = _fanRestPos + jitter;
            if (_shakeTimer <= 0f) _fanVisual.localPosition = _fanRestPos;
        }

        // Сброс серии по времени
        if (Time.time - _lastClickRealTime > _streakResetTime && _hitStreak > 0)
            _hitStreak = 0;

        if (_coolingSystem == null) return;
        if (_coolingSystem.State != FanState.Jammed &&
            _coolingSystem.State != FanState.Overheating) return;

        if (_inputRouter != null && !_inputRouter.IsActive(OperatorPosition.Fan)) return;
        if (GameManager.Instance != null && GameManager.Instance.IsInputBlocked()) return;

        if (Input.GetMouseButtonDown(0) && IsMouseOver())
            OnKnock();
    }

    private void OnKnock()
    {
        float now = Time.time;
        if (now - _lastClickTime <= _comboWindow) _hitStreak++;
        else _hitStreak = 0;

        _lastClickTime = now;
        _lastClickRealTime = now;

        float chance = Mathf.Min(_maxChance, _baseChance + _hitStreak * _increasePerStreak);
        bool success = Random.value < chance;

        if (_knockSound != null) _knockSound.Play();
        _shakeTimer = _shakeDuration;

        if (_debugLog)
            Debug.Log($"[FanKnock] Streak={_hitStreak}, chance={chance:F2}, success={success}");

        if (success)
        {
            _coolingSystem.OnKnockSuccess();
            _hitStreak = 0;
        }
    }

    private bool IsMouseOver()
    {
        if (Cam == null) return false;
        Ray ray = Cam.ScreenPointToRay(Input.mousePosition);
        var hits = Physics.RaycastAll(ray, 100f, ~0, QueryTriggerInteraction.Collide);
        foreach (var h in hits)
        {
            if (h.collider.gameObject == gameObject ||
                h.collider.transform.IsChildOf(transform))
                return true;
        }
        return false;
    }
}