using UnityEngine;

/// <summary>
/// Шаровая мышь АВМ. Две оси: yaw (X) — длина волны, pitch (Y) — высота.
/// Инерция только если игрок отпустил на ходу. Если остановился перед
/// отпусканием — шар остаётся на месте.
/// </summary>
[RequireComponent(typeof(Collider))]
public class TrackballMouse : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Меш шара — то, что визуально вращается. Пусто — вращается сам объект.")]
    [SerializeField] private Transform _ballMesh;
    [SerializeField] private OperatorInputRouter _inputRouter;
    [SerializeField] private Camera _camera;

    [Header("Drag")]
    [Tooltip("Градусов поворота на пиксель движения мыши.")]
    [SerializeField] private float _dragSensitivity = 0.6f;

    [Header("Inertia")]
    [Tooltip("Скорость затухания инерции (1/сек).")]
    [SerializeField] private float _inertiaDamping = 2f;
    [Tooltip("Ниже этой угловой скорости шар считается остановленным.")]
    [SerializeField] private float _stopThreshold = 20f;
    [Tooltip("Порог скорости, ниже которого инерция при отпускании НЕ запускается.")]
    [SerializeField] private float _releaseVelocityThreshold = 100f;
    [Tooltip("Максимальная угловая скорость (страховка от лагов).")]
    [SerializeField] private float _maxAngularSpeed = 1500f;

    [Header("Audio (optional)")]
    [SerializeField] private AudioSource _rollingSound;
    [SerializeField] private float _rollingVolumeMax = 0.4f;

    [Header("Debug")]
    [SerializeField] private bool _debugLog = false;

    // ---- Публичное ----
    public Vector2 FrameDelta { get; private set; }
    public Vector2 AngularVelocity => _angularVelocity;
    public bool IsGrabbed => _isGrabbed;

    // ---- Внутреннее ----
    private Vector2 _angularVelocity;
    private Vector2 _lastMousePos;
    private bool _isGrabbed;
    private bool _wasMoving;

    private Camera Cam => _camera != null ? _camera : Camera.main;

    private void Reset()
    {
        if (_ballMesh == null) _ballMesh = transform;
    }

    private void Update()
    {
        FrameDelta = Vector2.zero;

        bool canInteract = (_inputRouter == null || _inputRouter.IsActive(OperatorPosition.Screen))
                   && (GameManager.Instance == null
                       || (!GameManager.Instance.IsInputBlocked()
                           && !GameManager.Instance.IsPaused));
        // Начало захвата
        if (!_isGrabbed && canInteract && Input.GetMouseButtonDown(0) && IsMouseOver())
        {
            _isGrabbed = true;
            _lastMousePos = Input.mousePosition;
            _angularVelocity = Vector2.zero;
            _wasMoving = false;
            if (_debugLog) Debug.Log("[Trackball] Grab");
        }

        // Отпускание
        if (_isGrabbed && !Input.GetMouseButton(0))
        {
            _isGrabbed = false;
            if (_debugLog)
                Debug.Log($"[Trackball] Release, velocity={_angularVelocity.magnitude:F0}, " +
                          $"wasMoving={_wasMoving}");

            if (_angularVelocity.magnitude < _releaseVelocityThreshold)
                _angularVelocity = Vector2.zero;
        }

        // ---- Обработка ----
        if (_isGrabbed)
        {
            HandleDrag();
        }
        else if (_angularVelocity.sqrMagnitude > _stopThreshold * _stopThreshold)
        {
            HandleInertia();
        }
        else
        {
            _angularVelocity = Vector2.zero;
        }

        UpdateAudio();
    }

    private void HandleDrag()
    {
        Vector2 currentPos = Input.mousePosition;
        Vector2 delta = currentPos - _lastMousePos;
        _lastMousePos = currentPos;

        Vector2 rotationDelta = delta * _dragSensitivity;
        ApplyRotation(rotationDelta);
        FrameDelta = rotationDelta;

        if (Time.deltaTime > 0f)
            _angularVelocity = rotationDelta / Time.deltaTime;

        _wasMoving = delta.magnitude > 0.5f;
    }

    private void HandleInertia()
    {
        float dt = Time.deltaTime;

        _angularVelocity *= Mathf.Exp(-_inertiaDamping * dt);
        _angularVelocity = Vector2.ClampMagnitude(_angularVelocity, _maxAngularSpeed);

        Vector2 inertiaDelta = _angularVelocity * dt;
        ApplyRotation(inertiaDelta);
        FrameDelta = inertiaDelta;

        if (_angularVelocity.magnitude < _stopThreshold)
            _angularVelocity = Vector2.zero;
    }

    private void ApplyRotation(Vector2 degrees)
    {
        if (_ballMesh == null) return;
        Camera cam = Cam;
        if (cam == null) return;

        Quaternion yaw = Quaternion.AngleAxis(-degrees.x, cam.transform.up);
        Quaternion pitch = Quaternion.AngleAxis(degrees.y, cam.transform.right);

        _ballMesh.rotation = yaw * pitch * _ballMesh.rotation;
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

    private void UpdateAudio()
    {
        if (_rollingSound == null) return;
        float speed01 = Mathf.Clamp01(_angularVelocity.magnitude / _maxAngularSpeed);
        _rollingSound.volume = Mathf.Lerp(0f, _rollingVolumeMax, speed01);
        _rollingSound.pitch = Mathf.Lerp(0.8f, 1.4f, speed01);
    }
}