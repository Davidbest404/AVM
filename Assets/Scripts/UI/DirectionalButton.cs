using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Угловая кнопка направления (A/W/S/D). Визуально реагирует на доступность
/// направления из текущей позиции и перелёт. По клику вызывает
/// OperatorPositionController.OnDirectionalButtonPressed — то же, что нажатие клавиши.
/// </summary>
[RequireComponent(typeof(Button))]
[RequireComponent(typeof(CanvasGroup))]
public class DirectionalButton : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler
{
    [Header("Logic")]
    [Tooltip("Какое направление соответствует этой кнопке.")]
    [SerializeField] private KeyCode _key = KeyCode.A;

    [Tooltip("Ссылка на контроллер позиций.")]
    [SerializeField] private OperatorPositionController _controller;

    [Header("Visual")]
    [SerializeField] private CanvasGroup _canvasGroup;

    [Range(0f, 1f)][SerializeField] private float _idleAlpha = 0.5f;
    [Range(0f, 1f)][SerializeField] private float _hoverAlpha = 0.85f;
    [Range(0f, 1f)][SerializeField] private float _pressedAlpha = 1f;
    [Range(0f, 1f)][SerializeField] private float _disabledAlpha = 0.15f;

    [Tooltip("Скорость изменения прозрачности.")]
    [SerializeField] private float _fadeSpeed = 10f;

    [Tooltip("Насколько кнопка уменьшается при нажатии (0.95 = 5%).")]
    [Range(0.85f, 1f)]
    [SerializeField] private float _pressScale = 0.95f;

    [Tooltip("Скорость изменения масштаба.")]
    [SerializeField] private float _scaleSpeed = 15f;

    [Header("Audio")]
    [Tooltip("Опционально: звук нажатия.")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _pressSound;

    // ---- Внутреннее ----
    private Button _button;
    private RectTransform _rect;
    private bool _isHover;
    private bool _isPressed;

    private void Awake()
    {
        _button = GetComponent<Button>();
        _rect = GetComponent<RectTransform>();
        if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
        if (_audioSource == null) _audioSource = GetComponent<AudioSource>();
    }

    private void OnEnable()
    {
        _button.onClick.AddListener(HandleClick);
    }

    private void OnDisable()
    {
        _button.onClick.RemoveListener(HandleClick);
    }

    private void Update()
    {
        if (_controller == null || _canvasGroup == null) return;

        // Целевая прозрачность
        float targetAlpha;
        if (_controller.IsMoving || !_controller.IsDirectionAvailable(_key))
            targetAlpha = _disabledAlpha;
        else if (_isPressed)
            targetAlpha = _pressedAlpha;
        else if (_isHover)
            targetAlpha = _hoverAlpha;
        else
            targetAlpha = _idleAlpha;

        _canvasGroup.alpha = Mathf.Lerp(
            _canvasGroup.alpha, targetAlpha, Time.deltaTime * _fadeSpeed);

        // Нажатие — лёгкое сжатие
        float targetScale = _isPressed ? _pressScale : 1f;
        _rect.localScale = Vector3.Lerp(
            _rect.localScale, Vector3.one * targetScale, Time.deltaTime * _scaleSpeed);
    }

    private void HandleClick()
    {
        if (_controller == null) return;
        if (_controller.IsMoving) return;
        if (!_controller.IsDirectionAvailable(_key)) return;

        if (_audioSource != null && _pressSound != null)
            _audioSource.PlayOneShot(_pressSound);

        _controller.OnDirectionalButtonPressed(_key);
    }

    // ---- EventSystem ----

    public void OnPointerEnter(PointerEventData e) => _isHover = true;
    public void OnPointerExit(PointerEventData e) => _isHover = false;
    public void OnPointerDown(PointerEventData e) => _isPressed = true;
    public void OnPointerUp(PointerEventData e) => _isPressed = false;
}