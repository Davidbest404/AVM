using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Угловая кнопка A/W/S/D. Если направление недоступно — плавно исчезает
/// и перестаёт ловить клики. Так UI не засоряется лишними кнопками.
/// </summary>
[RequireComponent(typeof(Button))]
[RequireComponent(typeof(CanvasGroup))]
public class DirectionalButton : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler
{
    [Header("Logic")]
    [SerializeField] private KeyCode _key = KeyCode.A;
    [SerializeField] private OperatorPositionController _controller;

    [Header("Visual")]
    [SerializeField] private CanvasGroup _canvasGroup;

    [Range(0f, 1f)][SerializeField] private float _idleAlpha = 0.5f;
    [Range(0f, 1f)][SerializeField] private float _hoverAlpha = 0.85f;
    [Range(0f, 1f)][SerializeField] private float _pressedAlpha = 1.0f;
    [SerializeField] private float _fadeSpeed = 10f;

    [Tooltip("Полностью скрывать кнопку, когда направление недоступно. " +
             "Снят — кнопка остаётся тусклой (прозрачность = _disabledAlpha).")]
    [SerializeField] private bool _hideWhenUnavailable = true;

    [Tooltip("Прозрачность недоступной кнопки, если _hideWhenUnavailable = false.")]
    [Range(0f, 1f)][SerializeField] private float _disabledAlpha = 0.15f;

    [Tooltip("Насколько кнопка уменьшается при нажатии.")]
    [Range(0.85f, 1f)][SerializeField] private float _pressScale = 0.95f;
    [SerializeField] private float _scaleSpeed = 15f;

    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _pressSound;

    // ---- Внутреннее ----
    private Button _button;
    private RectTransform _rect;
    private bool _isHover;
    private bool _isPressed;
    private bool _available = true;

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

        // Доступна ли кнопка сейчас?
        bool available = !_controller.IsMoving
                      && (GameManager.Instance == null || !GameManager.Instance.IsPaused)
                      && _controller.IsDirectionAvailable(_key);

        // Если стали недоступны — сбросить hover/pressed
        if (!available)
        {
            _isHover = false;
            _isPressed = false;
        }
        _available = available;

        // Целевая прозрачность и raycast
        float targetAlpha;
        bool raycast;

        if (!available)
        {
            if (_hideWhenUnavailable)
            {
                targetAlpha = 0f;
                raycast = false;
            }
            else
            {
                targetAlpha = _disabledAlpha;
                raycast = false;
            }
        }
        else
        {
            raycast = true;
            if (_isPressed) targetAlpha = _pressedAlpha;
            else if (_isHover) targetAlpha = _hoverAlpha;
            else targetAlpha = _idleAlpha;
        }

        _canvasGroup.alpha = Mathf.Lerp(
            _canvasGroup.alpha, targetAlpha, Time.deltaTime * _fadeSpeed);

        // Кнопка не должна ловить клики, пока невидима
        _canvasGroup.blocksRaycasts = raycast;
        _canvasGroup.interactable = raycast;

        // Сжатие при нажатии
        float targetScale = _isPressed ? _pressScale : 1f;
        _rect.localScale = Vector3.Lerp(
            _rect.localScale, Vector3.one * targetScale, Time.deltaTime * _scaleSpeed);
    }

    private void HandleClick()
    {
        if (_controller == null) return;
        if (_controller.IsMoving) return;
        if (GameManager.Instance != null && GameManager.Instance.IsPaused) return;
        if (!_controller.IsDirectionAvailable(_key)) return;

        if (_audioSource != null && _pressSound != null)
            _audioSource.PlayOneShot(_pressSound);

        _controller.OnDirectionalButtonPressed(_key);
    }

    // ---- EventSystem ----

    public void OnPointerEnter(PointerEventData e) { if (_available) _isHover = true; }
    public void OnPointerExit(PointerEventData e) { _isHover = false; }
    public void OnPointerDown(PointerEventData e) { if (_available) _isPressed = true; }
    public void OnPointerUp(PointerEventData e) { _isPressed = false; }
}