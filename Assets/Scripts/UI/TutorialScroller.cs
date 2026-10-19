using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Автоскролл туториального текста на панели паузы.
/// Скроллит вниз пока не дойдёт до конца, потом возвращается наверх.
/// Работает при Time.timeScale = 0 (использует unscaledDeltaTime).
/// </summary>
[RequireComponent(typeof(ScrollRect))]
public class TutorialScroller : MonoBehaviour
{
    [Header("Auto Scroll")]
    [SerializeField] private bool _autoScroll = true;
    [Tooltip("Скорость скролла (единиц normalizedPosition в секунду).")]
    [SerializeField] private float _scrollSpeed = 0.04f;
    [Tooltip("Пауза на верху и внизу перед разворотом (сек).")]
    [SerializeField] private float _pauseAtEnds = 2f;

    private ScrollRect _scrollRect;
    private float _direction = -1f; // -1 = вниз, +1 = вверх
    private float _pauseTimer;

    private void Awake()
    {
        _scrollRect = GetComponent<ScrollRect>();
        if (_scrollRect != null) _scrollRect.verticalNormalizedPosition = 1f;
    }

    private void Update()
    {
        if (!_autoScroll || _scrollRect == null) return;

        if (_pauseTimer > 0f)
        {
            _pauseTimer -= Time.unscaledDeltaTime;
            return;
        }

        float delta = _direction * _scrollSpeed * Time.unscaledDeltaTime;
        float pos = _scrollRect.verticalNormalizedPosition + delta;

        if (pos <= 0f)
        {
            pos = 0f;
            _direction = 1f;
            _pauseTimer = _pauseAtEnds;
        }
        else if (pos >= 1f)
        {
            pos = 1f;
            _direction = -1f;
            _pauseTimer = _pauseAtEnds;
        }

        _scrollRect.verticalNormalizedPosition = pos;
    }

    /// <summary>Ручной скролл пользователем — остановить автоскролл.</summary>
    public void OnUserScrolled()
    {
        _autoScroll = false;
    }
}