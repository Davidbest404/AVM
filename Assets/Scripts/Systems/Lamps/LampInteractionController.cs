using UnityEngine;

/// <summary>
/// Единый контроллер взаимодействия с лампами на позиции "Lamps".
/// Управляет тремя состояниями: Idle / Unwinding / Placing.
/// </summary>
public class LampInteractionController : MonoBehaviour
{
    private enum InteractionState { Idle, Unwinding, Placing }

    [Header("References")]
    [SerializeField] private OperatorInputRouter _inputRouter;
    [SerializeField] private Camera _camera;

    [Header("Unwinding")]
    [Tooltip("Сколько пикселей мыши нужно 'пройти', чтобы выкрутить лампу полностью.")]
    [SerializeField] private float _unwindThresholdPixels = 800f;

    [Header("Placing")]
    [Tooltip("Точка, задающая плоскость для следования лампы за курсором.")]
    [SerializeField] private Transform _placingPlaneAnchor;

    [Tooltip("Слои, где ищем сокет при вставке.")]
    [SerializeField] private LayerMask _socketLayer = ~0;

    [Header("Raycast")]
    [SerializeField] private float _maxRayDistance = 100f;

    [Header("Debug")]
    [SerializeField] private bool _debugLog = false;

    // ---- Состояние ----
    private InteractionState _state = InteractionState.Idle;
    private Lamp _unwindingLamp;
    private Vector2 _lastMousePos;
    private float _accumulatedPixels;

    private Lamp _placingLamp;
    private Plane _placingPlane;

    public bool IsBusy => _state != InteractionState.Idle;

    private Camera Cam => _camera != null ? _camera : Camera.main;

    private void Update()
    {
        // Если сейчас «заняты» — обрабатываем своё состояние, игнорируя блокировки
        switch (_state)
        {
            case InteractionState.Unwinding: HandleUnwinding(); return;
            case InteractionState.Placing: HandlePlacing(); return;
        }

        // Idle:
        if (GameManager.Instance != null
            && (GameManager.Instance.IsInputBlocked() || GameManager.Instance.IsPaused)) return;
        if (_inputRouter == null || !_inputRouter.IsActive(OperatorPosition.Lamps)) return;

        HandleIdle();
    }

    // ============================================================
    //  IDLE
    // ============================================================

    private void HandleIdle()
    {
        if (!Input.GetMouseButtonDown(0)) return;

        // 1. Клик по лампе (только сломанной) → выкручивание
        if (TryRaycastLamp(out Lamp lamp))
        {
            if (lamp.IsInstalled &&
                (lamp.State == LampState.Damaged || lamp.State == LampState.Faulty))
            {
                BeginUnwind(lamp);
            }
            return;
        }

        // 2. Клик по ящику → новая лампа за курсором
        if (TryRaycastLampBox(out LampBox box))
        {
            BeginPlacement(box);
        }
    }

    // ============================================================
    //  UNWINDING
    // ============================================================

    private void BeginUnwind(Lamp lamp)
    {
        _state = InteractionState.Unwinding;
        _unwindingLamp = lamp;
        _accumulatedPixels = 0f;
        _lastMousePos = Input.mousePosition;
        lamp.ResetUnwindVisual();
        lamp.Audio?.SetUnwinding(true);

        if (_debugLog) Debug.Log($"[LampCtrl] BeginUnwind {lamp.name}");
    }

    private void HandleUnwinding()
    {
        if (_unwindingLamp == null) { CancelUnwind(); return; }

        // Отпустили ЛКМ до 100% — сброс
        if (!Input.GetMouseButton(0))
        {
            CancelUnwind();
            return;
        }

        // Накапливаем пройденный путь мыши
        Vector2 mouse = Input.mousePosition;
        _accumulatedPixels += (mouse - _lastMousePos).magnitude;
        _lastMousePos = mouse;

        float progress = Mathf.Clamp01(_accumulatedPixels / _unwindThresholdPixels);
        _unwindingLamp.SetUnwindProgress(progress);

        if (progress >= 1f)
            FinishUnwind();
    }

    private void CancelUnwind()
    {
        if (_debugLog) Debug.Log("[LampCtrl] CancelUnwind");
        if (_unwindingLamp != null) _unwindingLamp.ResetUnwindVisual();
        if (_unwindingLamp != null) _unwindingLamp.Audio?.SetUnwinding(false);
        _unwindingLamp = null;
        _accumulatedPixels = 0f;
        _state = InteractionState.Idle;
    }

    private void FinishUnwind()
    {
        Lamp lamp = _unwindingLamp;
        _unwindingLamp = null;
        _accumulatedPixels = 0f;
        _state = InteractionState.Idle;

        if (_debugLog) Debug.Log($"[LampCtrl] FinishUnwind {lamp.name}");

        lamp.Audio?.SetUnwinding(false);
        lamp.DetachFromSocket();
        lamp.StartFalling();
        LampSystem.Instance?.NotifyLampsChanged();
    }

    // ============================================================
    //  PLACING
    // ============================================================

    private void BeginPlacement(LampBox box)
    {
        Lamp lamp = box.SpawnLamp();
        if (lamp == null) return;

        _placingLamp = lamp;
        _state = InteractionState.Placing;

        // Плоскость: перпендикулярна forward камеры, через точку-анкор (или через ящик)
        Vector3 anchor = _placingPlaneAnchor != null
            ? _placingPlaneAnchor.position
            : box.transform.position;
        _placingPlane = new Plane(Cam.transform.forward, anchor);

        MoveLampToCursor(lamp);

        if (_debugLog) Debug.Log("[LampCtrl] BeginPlacement");
    }

    private void HandlePlacing()
    {
        if (_placingLamp == null) { _state = InteractionState.Idle; return; }

        if (Input.GetMouseButton(0))
        {
            MoveLampToCursor(_placingLamp);
            return;
        }

        if (Input.GetMouseButtonUp(0))
            TryInstallPlacing();
    }

    private void MoveLampToCursor(Lamp lamp)
    {
        if (Cam == null) return;
        Ray ray = Cam.ScreenPointToRay(Input.mousePosition);
        if (_placingPlane.Raycast(ray, out float dist))
        {
            lamp.transform.position = ray.GetPoint(dist);
        }
    }

    private void TryInstallPlacing()
    {
        Lamp lamp = _placingLamp;
        _placingLamp = null;
        _state = InteractionState.Idle;

        LampSocket socket = FindSocketUnderCursor();
        if (socket != null && socket.IsEmpty)
        {
            socket.SetLamp(lamp);
            lamp.InstallTo(socket);
            LampSystem.Instance?.NotifyLampsChanged();
            if (_debugLog) Debug.Log($"[LampCtrl] Installed into {socket.name}");
        }
        else
        {
            if (_debugLog) Debug.Log("[LampCtrl] Miss — lamp falling");
            lamp.StartFalling();
        }
    }

    private LampSocket FindSocketUnderCursor()
    {
        if (Cam == null) return null;
        Ray ray = Cam.ScreenPointToRay(Input.mousePosition);
        var hits = Physics.RaycastAll(ray, _maxRayDistance, _socketLayer);
        LampSocket closest = null;
        float minDist = float.MaxValue;
        foreach (var h in hits)
        {
            var sock = h.collider.GetComponentInParent<LampSocket>();
            if (sock != null && h.distance < minDist)
            {
                minDist = h.distance;
                closest = sock;
            }
        }
        return closest;
    }

    // ============================================================
    //  RAYCAST HELPERS
    // ============================================================

    private bool TryRaycastLamp(out Lamp lamp)
    {
        lamp = null;
        if (Cam == null) return false;

        Ray ray = Cam.ScreenPointToRay(Input.mousePosition);
        var hits = Physics.RaycastAll(ray, _maxRayDistance);
        float minDist = float.MaxValue;
        foreach (var h in hits)
        {
            var l = h.collider.GetComponentInParent<Lamp>();
            if (l != null && h.distance < minDist)
            {
                minDist = h.distance;
                lamp = l;
            }
        }
        return lamp != null;
    }

    private bool TryRaycastLampBox(out LampBox box)
    {
        box = null;
        if (Cam == null) return false;

        Ray ray = Cam.ScreenPointToRay(Input.mousePosition);
        var hits = Physics.RaycastAll(ray, _maxRayDistance);
        float minDist = float.MaxValue;
        foreach (var h in hits)
        {
            var b = h.collider.GetComponentInParent<LampBox>();
            if (b != null && h.distance < minDist)
            {
                minDist = h.distance;
                box = b;
            }
        }
        return box != null;
    }
}