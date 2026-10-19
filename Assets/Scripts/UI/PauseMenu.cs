using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Пауза на ESC. Останавливает Time.timeScale и прогресс.
/// Показывает меню: Продолжить / Настройки / Главное меню / Выйти.
/// В настройках — переключение на панель с ползунками.
/// </summary>
public class PauseMenu : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject _menuRoot;
    [SerializeField] private GameObject _mainPanel;
    [SerializeField] private GameObject _settingsPanel;

    [Header("Buttons")]
    [SerializeField] private Button _resumeButton;
    [SerializeField] private Button _settingsButton;
    [SerializeField] private Button _mainMenuButton;
    [SerializeField] private Button _quitButton;
    [SerializeField] private Button _settingsBackButton;

    [Header("Scene")]
    [SerializeField] private string _mainMenuSceneName = "MainMenu";

    [Header("Debug")]
    [SerializeField] private bool _debugLog = false;

    private bool _isOpen;

    private void Awake()
    {
        // Сразу спрятать
        if (_menuRoot != null) _menuRoot.SetActive(false);

        // Подписки
        if (_resumeButton != null) _resumeButton.onClick.AddListener(OnResume);
        if (_settingsButton != null) _settingsButton.onClick.AddListener(OnOpenSettings);
        if (_mainMenuButton != null) _mainMenuButton.onClick.AddListener(OnMainMenu);
        if (_quitButton != null) _quitButton.onClick.AddListener(OnQuit);
        if (_settingsBackButton != null) _settingsBackButton.onClick.AddListener(OnCloseSettings);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (GameManager.Instance != null && GameManager.Instance.IsGameOver) return;
            if (_isOpen) CloseMenu();
            else OpenMenu();
        }
    }

    // ---- Открытие / закрытие ----

    public void OpenMenu()
    {
        _isOpen = true;
        if (_menuRoot != null) _menuRoot.SetActive(true);
        if (_mainPanel != null) _mainPanel.SetActive(true);
        if (_settingsPanel != null) _settingsPanel.SetActive(false);

        Time.timeScale = 0f;
        if (GameManager.Instance != null) GameManager.Instance.SetPaused(true);

        // Выделить кнопку "Продолжить"
        if (EventSystem.current != null && _resumeButton != null)
            EventSystem.current.SetSelectedGameObject(_resumeButton.gameObject);

        if (_debugLog) Debug.Log("[PauseMenu] Open");
    }

    public void CloseMenu()
    {
        _isOpen = false;
        if (_menuRoot != null) _menuRoot.SetActive(false);

        Time.timeScale = 1f;
        if (GameManager.Instance != null) GameManager.Instance.SetPaused(false);

        if (_debugLog) Debug.Log("[PauseMenu] Close");
    }

    // ---- Кнопки ----

    private void OnResume()
    {
        CloseMenu();
    }

    private void OnOpenSettings()
    {
        if (_mainPanel != null) _mainPanel.SetActive(false);
        if (_settingsPanel != null) _settingsPanel.SetActive(true);

        if (EventSystem.current != null && _settingsBackButton != null)
            EventSystem.current.SetSelectedGameObject(_settingsBackButton.gameObject);
    }

    private void OnCloseSettings()
    {
        if (_settingsPanel != null) _settingsPanel.SetActive(false);
        if (_mainPanel != null) _mainPanel.SetActive(true);

        if (EventSystem.current != null && _settingsButton != null)
            EventSystem.current.SetSelectedGameObject(_settingsButton.gameObject);
    }

    private void OnMainMenu()
    {
        Time.timeScale = 1f;
        if (GameManager.Instance != null) GameManager.Instance.SetPaused(false);
        SceneManager.LoadScene(_mainMenuSceneName);
    }

    private void OnQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}