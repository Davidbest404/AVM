using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Shows an end screen when the game is won or lost.
/// Pauses the game, fades in the panel, and offers Restart / Main Menu.
/// </summary>
public class EndScreenManager : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject _panel;
    [Tooltip("Canvas Group на панели — для fade-in. Может быть null.")]
    [SerializeField] private CanvasGroup _panelGroup;

    [Header("Text (TextMeshPro)")]
    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private TMP_Text _messageText;

    [Header("Colors")]
    [SerializeField] private Color _winTitleColor = new Color(0.3f, 1f, 0.4f);
    [SerializeField] private Color _loseTitleColor = new Color(1f, 0.3f, 0.2f);

    [Header("Buttons")]
    [SerializeField] private Button _restartButton;
    [SerializeField] private Button _mainMenuButton;
    [SerializeField] private string _mainMenuSceneName = "MainMenu";

    [Header("English Texts")]
    [SerializeField] private string _winTitle = "YOU WIN!";
    [SerializeField] private string _winMessage = "The AVM finished the calculation in time.\nWell done, Operator!";
    [SerializeField] private string _loseTitle = "YOU LOSE";
    [SerializeField] private string _loseMessage = "The AVM ran out of time.\nThe calculation failed.";

    [Header("Fade")]
    [SerializeField] private float _fadeDuration = 0.5f;

    [Header("Debug")]
    [SerializeField] private bool _debugLog = false;

    private bool _showing;

    private void Awake()
    {
        if (_panel != null) _panel.SetActive(false);

        if (_restartButton != null) _restartButton.onClick.AddListener(OnRestart);
        if (_mainMenuButton != null) _mainMenuButton.onClick.AddListener(OnMainMenu);
    }

    private void Start()
    {
        if (GameManager.Instance == null)
        {
            Debug.LogError("[EndScreenManager] GameManager.Instance = null — события не подпишутся.");
            return;
        }

        GameManager.Instance.OnGameWin += HandleWin;
        GameManager.Instance.OnGameLose += HandleLose;
    }

    private void OnDestroy()
    {
        if (GameManager.Instance == null) return;
        GameManager.Instance.OnGameWin -= HandleWin;
        GameManager.Instance.OnGameLose -= HandleLose;
    }

    private void HandleWin() => StartCoroutine(ShowPanel(true));
    private void HandleLose() => StartCoroutine(ShowPanel(false));

    private IEnumerator ShowPanel(bool win)
    {
        if (_showing) yield break;
        _showing = true;

        if (_debugLog) Debug.Log($"[EndScreenManager] ShowPanel(win={win})");

        // Останавливаем игру
        Time.timeScale = 0f;
        if (GameManager.Instance != null) GameManager.Instance.SetPaused(true);

        if (_panel != null) _panel.SetActive(true);

        // Заполняем тексты
        if (_titleText != null)
        {
            _titleText.text = win ? _winTitle : _loseTitle;
            _titleText.color = win ? _winTitleColor : _loseTitleColor;
        }
        if (_messageText != null)
            _messageText.text = win ? _winMessage : _loseMessage;

        // Fade-in через unscaledDeltaTime (timeScale = 0)
        if (_panelGroup != null && _fadeDuration > 0f)
        {
            _panelGroup.alpha = 0f;
            _panelGroup.blocksRaycasts = true;
            float t = 0f;
            while (t < _fadeDuration)
            {
                t += Time.unscaledDeltaTime;
                _panelGroup.alpha = Mathf.Lerp(0f, 1f, t / _fadeDuration);
                yield return null;
            }
            _panelGroup.alpha = 1f;
        }
        else if (_panelGroup != null)
        {
            _panelGroup.alpha = 1f;
        }
    }

    private void OnRestart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void OnMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(_mainMenuSceneName);
    }
}