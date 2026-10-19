using UnityEngine;

/// <summary>
/// Кнопка перемотки. Всегда интерактивна. Управляет только визуалом
/// и сообщает TapeSystem, что игрок держит кнопку (через SetRewinding).
/// </summary>
public class TapeRewindButton : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TapeSystem _tapeSystem;
    [SerializeField] private OperatorInputRouter _inputRouter;
    [SerializeField] private Camera _camera;

    [Header("Button Visual")]
    [SerializeField] private Transform _buttonVisual;
    [SerializeField] private float _pressDepth = 0.02f;

    [Header("Emission (URP)")]
    [SerializeField] private Color _workingIdleColor = new Color(0.15f, 0.15f, 0.15f);
    [SerializeField] private Color _workingHeldColor = new Color(0.4f, 0.4f, 0.5f);
    [Tooltip("Цвет при N=0 (зажевывание только началось).")]
    [SerializeField] private Color _jamStartColor = new Color(0.3f, 1f, 0.4f);
    [Tooltip("Цвет при N=Max (полностью зажёвана).")]
    [SerializeField] private Color _jamMaxColor = new Color(0.6f, 0.1f, 0.05f);

    [Header("Debug")]
    [SerializeField] private bool _debugLog = false;

    private bool _isHolding;
    private Vector3 _restPos;
    private Renderer _buttonRenderer;
    private MaterialPropertyBlock _mpb;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private Camera Cam => _camera != null ? _camera : Camera.main;

    private void Awake()
    {
        _mpb = new MaterialPropertyBlock();
        if (_buttonVisual != null)
        {
            _restPos = _buttonVisual.localPosition;
            _buttonRenderer = _buttonVisual.GetComponentInChildren<Renderer>();
        }
        if (_buttonRenderer == null) _buttonRenderer = GetComponentInChildren<Renderer>();
    }

    private void Update()
    {
        if (_tapeSystem == null) return;

        // Не на позиции ленты — отпускаем
        if (_inputRouter != null && !_inputRouter.IsActive(OperatorPosition.Tape)
            || (GameManager.Instance != null && GameManager.Instance.IsPaused))
        {
            if (_isHolding) ReleaseHold();
            UpdateEmission();
            return;
        }

        if (!_isHolding && Input.GetMouseButtonDown(0) && IsMouseOverButton())
        {
            _isHolding = true;
            PressVisual(true);
            if (_debugLog) Debug.Log("[TapeButton] Hold start");
        }

        bool stillHolding = _isHolding
            && Input.GetMouseButton(0)
            && IsMouseOverButton();

        if (!stillHolding && _isHolding)
        {
            if (_debugLog) Debug.Log("[TapeButton] Released");
            ReleaseHold();
        }

        UpdateEmission();
    }

    private void ReleaseHold()
    {
        _isHolding = false;
        PressVisual(false);
    }

    private void PressVisual(bool pressed)
    {
        if (_buttonVisual != null)
        {
            _buttonVisual.localPosition = pressed
                ? _restPos + Vector3.down * _pressDepth
                : _restPos;
        }
        // Колесо назад, пока держим (и в Working, и в Jammed)
        if (_tapeSystem != null) _tapeSystem.SetRewinding(pressed);
    }

    private void UpdateEmission()
    {
        if (_buttonRenderer == null) return;

        Color target;
        if (_tapeSystem.State == TapeState.Working)
        {
            target = _isHolding ? _workingHeldColor : _workingIdleColor;
        }
        else
        {
            // Jammed: цвет по N/Max
            float t = Mathf.Clamp01(
                _tapeSystem.CurrentJamValue / Mathf.Max(0.001f, _tapeSystem.MaxJamValue));
            target = Color.Lerp(_jamStartColor, _jamMaxColor, t);
        }

        ApplyEmission(target);
    }

    private void ApplyEmission(Color c)
    {
        _buttonRenderer.GetPropertyBlock(_mpb);
        _mpb.SetColor(BaseColorId, c);
        _mpb.SetColor(ColorId, c);
        _mpb.SetColor(EmissionColorId, c * 3f);
        _buttonRenderer.SetPropertyBlock(_mpb);
    }

    private bool IsMouseOverButton()
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