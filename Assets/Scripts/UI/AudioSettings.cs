using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

/// <summary>
/// Настройки громкости через AudioMixer + сохранение в PlayerPrefs.
/// Слайдеры 0..1 → dB корректно (Log10 * 20).
/// Если AudioMixer или параметр не настроены — пишет warning в консоль.
/// </summary>
public class AudioSettings : MonoBehaviour
{
    [Header("Mixer")]
    [SerializeField] private AudioMixer _mixer;

    [Header("Sliders (0..1)")]
    [SerializeField] private Slider _masterSlider;
    [SerializeField] private Slider _musicSlider;
    [SerializeField] private Slider _sfxSlider;

    [Header("PlayerPrefs Keys")]
    [SerializeField] private string _masterKey = "vol_master";
    [SerializeField] private string _musicKey = "vol_music";
    [SerializeField] private string _sfxKey = "vol_sfx";

    [Header("Debug")]
    [SerializeField] private bool _debugLog = true;

    private const string MasterParam = "MasterVolume";
    private const string MusicParam = "MusicVolume";
    private const string SFXParam = "SFXVolume";
    private const float MinDB = -80f;

    private void Start()
    {
        float master = PlayerPrefs.GetFloat(_masterKey, 0.8f);
        float music = PlayerPrefs.GetFloat(_musicKey, 0.6f);
        float sfx = PlayerPrefs.GetFloat(_sfxKey, 0.9f);

        if (_masterSlider != null) _masterSlider.value = master;
        if (_musicSlider != null) _musicSlider.value = music;
        if (_sfxSlider != null) _sfxSlider.value = sfx;

        if (_masterSlider != null) _masterSlider.onValueChanged.AddListener(OnMasterChanged);
        if (_musicSlider != null) _musicSlider.onValueChanged.AddListener(OnMusicChanged);
        if (_sfxSlider != null) _sfxSlider.onValueChanged.AddListener(OnSFXChanged);

        // Применяем сохранённые значения при старте
        ApplyAll(master, music, sfx);

        // Диагностика
        if (_mixer == null)
            Debug.LogError("[AudioSettings] _mixer не назначен — Music/SFX не будут работать. Master через AudioListener.");
        if (_masterSlider == null)
            Debug.LogWarning("[AudioSettings] _masterSlider не назначен");
        if (_musicSlider == null)
            Debug.LogWarning("[AudioSettings] _musicSlider не назначен");
        if (_sfxSlider == null)
            Debug.LogWarning("[AudioSettings] _sfxSlider не назначен");
    }

    private void OnDestroy()
    {
        if (_masterSlider != null) _masterSlider.onValueChanged.RemoveListener(OnMasterChanged);
        if (_musicSlider != null) _musicSlider.onValueChanged.RemoveListener(OnMusicChanged);
        if (_sfxSlider != null) _sfxSlider.onValueChanged.RemoveListener(OnSFXChanged);
    }

    private void OnMasterChanged(float v)
    {
        ApplyMaster(v);
        PlayerPrefs.SetFloat(_masterKey, v);
        PlayerPrefs.Save();
    }

    private void OnMusicChanged(float v)
    {
        ApplyMixer(MusicParam, v);
        PlayerPrefs.SetFloat(_musicKey, v);
        PlayerPrefs.Save();
    }

    private void OnSFXChanged(float v)
    {
        ApplyMixer(SFXParam, v);
        PlayerPrefs.SetFloat(_sfxKey, v);
        PlayerPrefs.Save();
    }

    private void ApplyAll(float master, float music, float sfx)
    {
        ApplyMaster(master);
        ApplyMixer(MusicParam, music);
        ApplyMixer(SFXParam, sfx);
    }

    /// <summary>Master — идёт через AudioMixer, если он есть. Иначе — AudioListener.volume.</summary>
    private void ApplyMaster(float v)
    {
        if (_mixer != null)
        {
            ApplyMixer(MasterParam, v);
        }
        else
        {
            AudioListener.volume = Mathf.Clamp01(v);
            if (_debugLog) Debug.Log($"[AudioSettings] Master (AudioListener.volume) = {v:F2}");
        }
    }

    private void ApplyMixer(string param, float linear01)
    {
        if (_mixer == null)
        {
            if (_debugLog) Debug.LogWarning($"[AudioSettings] _mixer = null, '{param}' не применён");
            return;
        }

        float db = LinearToDB(linear01);
        bool ok = _mixer.SetFloat(param, db);

        if (!ok)
        {
            Debug.LogError($"[AudioSettings] Параметр '{param}' не найден в AudioMixer. " +
                           $"Открой AudioMixer → выдели группу → правый клик по Volume → Expose. " +
                           $"Затем переименуй в Exposed Parameters в '{param}'.");
            return;
        }

        if (_debugLog)
            Debug.Log($"[AudioSettings] '{param}': linear={linear01:F2} → {db:F1} dB");
    }

    /// <summary>Правильная конверсия linear (0..1) → dB. 0 = -80 (тишина), 1 = 0 dB.</summary>
    private static float LinearToDB(float linear01)
    {
        if (linear01 <= 0.0001f) return MinDB;
        return Mathf.Log10(Mathf.Clamp01(linear01)) * 20f;
    }
}