using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Главное меню: кнопки Играть / Выйти + плавный переход на игровую сцену.
/// </summary>
public class MainMenu : MonoBehaviour
{
    [Header("Scenes")]
    [Tooltip("Имя игровой сцены (как в Build Settings).")]
    [SerializeField] private string _gameSceneName = "Main";

    [Header("Buttons")]
    [SerializeField] private Button _playButton;
    [SerializeField] private Button _quitButton;

    [Header("Fade (optional)")]
    [Tooltip("CanvasGroup чёрной панели на весь экран для fade-out. Может быть null.")]
    [SerializeField] private CanvasGroup _fadeOverlay;
    [SerializeField] private float _fadeDuration = 0.5f;

    [Header("Audio (optional)")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _clickSound;

    private bool _transitioning;

    private void Awake()
    {
        if (_playButton != null) _playButton.onClick.AddListener(OnPlayClicked);
        if (_quitButton != null) _quitButton.onClick.AddListener(OnQuitClicked);

        // Fade overlay начинается прозрачным
        if (_fadeOverlay != null)
        {
            _fadeOverlay.alpha = 0f;
            _fadeOverlay.blocksRaycasts = false;
        }
    }

    private void OnPlayClicked()
    {
        if (_transitioning) return;
        PlayClick();
        StartCoroutine(LoadGameScene());
    }

    private void OnQuitClicked()
    {
        if (_transitioning) return;
        PlayClick();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void PlayClick()
    {
        if (_audioSource != null && _clickSound != null)
            _audioSource.PlayOneShot(_clickSound);
    }

    private IEnumerator LoadGameScene()
    {
        _transitioning = true;

        // Блокируем кнопки
        if (_playButton != null) _playButton.interactable = false;
        if (_quitButton != null) _quitButton.interactable = false;

        // Fade-out
        if (_fadeOverlay != null && _fadeDuration > 0f)
        {
            _fadeOverlay.blocksRaycasts = true;
            float t = 0f;
            while (t < _fadeDuration)
            {
                t += Time.unscaledDeltaTime;
                _fadeOverlay.alpha = Mathf.Lerp(0f, 1f, t / _fadeDuration);
                yield return null;
            }
            _fadeOverlay.alpha = 1f;
        }

        // Асинхронная загрузка — Unity не подвиснет
        yield return null; // даём Unity отрисовать fade
        AsyncOperation op = SceneManager.LoadSceneAsync(_gameSceneName);
        if (op == null)
        {
            Debug.LogError($"[MainMenu] Сцена '{_gameSceneName}' не найдена в Build Settings!");
            _transitioning = false;
            if (_playButton != null) _playButton.interactable = true;
            if (_quitButton != null) _quitButton.interactable = true;
            yield break;
        }

        while (!op.isDone)
            yield return null;
    }
}