using TMPro;
using UnityEngine;

/// <summary>
/// Часы с обратным отсчётом: показывает оставшееся время от 5:00 до 0:00.
/// Формат M:SS. Меняет цвет при приближении к концу.
/// </summary>
public class CountdownClock : MonoBehaviour
{
    [Header("References")]
    [Tooltip("GameManager. Если пусто — найдёт автоматически.")]
    [SerializeField] private GameManager _gameManager;

    [Tooltip("TextMeshPro для отображения времени.")]
    [SerializeField] private TMP_Text _timeText;

    [Header("Colors")]
    [SerializeField] private Color _normalColor = new Color(0.85f, 0.85f, 0.85f);
    [SerializeField] private Color _warningColor = new Color(1f, 0.85f, 0.2f);
    [SerializeField] private Color _dangerColor = new Color(1f, 0.25f, 0.2f);

    [Header("Thresholds")]
    [Tooltip("За сколько секунд до конца перекрашивать в жёлтый.")]
    [SerializeField] private float _warningThreshold = 60f;
    [Tooltip("За сколько секунд до конца перекрашивать в красный.")]
    [SerializeField] private float _dangerThreshold = 20f;

    [Header("Blink")]
    [Tooltip("Мигать, когда осталось мало времени.")]
    [SerializeField] private bool _blinkOnDanger = true;
    [SerializeField] private float _blinkSpeed = 3f;

    private void Awake()
    {
        if (_gameManager == null) _gameManager = GameManager.Instance;
    }

    private void Start()
    {
        if (_gameManager == null)
        {
            Debug.LogError("[CountdownClock] GameManager не найден.");
            enabled = false;
            return;
        }

        if (_timeText == null)
        {
            Debug.LogError("[CountdownClock] _timeText не назначен.");
            enabled = false;
            return;
        }

        UpdateDisplay();
    }

    private void Update()
    {
        if (_gameManager == null || _timeText == null) return;
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        float total = 300f;
        float remaining = Mathf.Max(0f, total - _gameManager.CurrentTime);

        int minutes = Mathf.FloorToInt(remaining / 60f);
        int seconds = Mathf.FloorToInt(remaining % 60f);

        _timeText.text = $"{minutes}:{seconds:00}";

        // Цвет
        Color targetColor;
        if (remaining <= _dangerThreshold) targetColor = _dangerColor;
        else if (remaining <= _warningThreshold) targetColor = _warningColor;
        else targetColor = _normalColor;

        // Мигание в danger
        if (_blinkOnDanger && remaining <= _dangerThreshold && !_gameManager.IsGameOver)
        {
            float blink = 0.5f + 0.5f * Mathf.Sin(Time.time * _blinkSpeed * Mathf.PI * 2f);
            targetColor = Color.Lerp(targetColor * 0.3f, targetColor, blink);
        }

        _timeText.color = targetColor;
    }
}