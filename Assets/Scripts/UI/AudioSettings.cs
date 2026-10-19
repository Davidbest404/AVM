using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

/// <summary>
/// Настройки громкости через AudioMixer + сохранение в PlayerPrefs.
/// Слайдеры 0..1 → dB -80..0.
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

    private const string MasterParam = "MasterVolume";
    private const string MusicParam = "MusicVolume";
    private const string SFXParam = "SFXVolume";
    private const float MinDB = -80f;
    private const float MaxDB = 0f;

    private void Start()
    {
        // Загружаем сохранённые значения
        float master = PlayerPrefs.GetFloat(_masterKey, 0.8f);
        float music = PlayerPrefs.GetFloat(_musicKey, 0.6f);
        float sfx = PlayerPrefs.GetFloat(_sfxKey, 0.9f);

        if (_masterSlider != null) _masterSlider.value = master;
        if (_musicSlider != null) _musicSlider.value = music;
        if (_sfxSlider != null) _sfxSlider.value = sfx;

        Apply(master, music, sfx);

        // Подписки
        if (_masterSlider != null) _masterSlider.onValueChanged.AddListener(OnMasterChanged);
        if (_musicSlider != null) _musicSlider.onValueChanged.AddListener(OnMusicChanged);
        if (_sfxSlider != null) _sfxSlider.onValueChanged.AddListener(OnSFXChanged);
    }

    private void OnDestroy()
    {
        if (_masterSlider != null) _masterSlider.onValueChanged.RemoveListener(OnMasterChanged);
        if (_musicSlider != null) _musicSlider.onValueChanged.RemoveListener(OnMusicChanged);
        if (_sfxSlider != null) _sfxSlider.onValueChanged.RemoveListener(OnSFXChanged);
    }

    private void OnMasterChanged(float v)
    {
        SetVolume(MasterParam, v);
        PlayerPrefs.SetFloat(_masterKey, v);
        PlayerPrefs.Save();
    }

    private void OnMusicChanged(float v)
    {
        SetVolume(MusicParam, v);
        PlayerPrefs.SetFloat(_musicKey, v);
        PlayerPrefs.Save();
    }

    private void OnSFXChanged(float v)
    {
        SetVolume(SFXParam, v);
        PlayerPrefs.SetFloat(_sfxKey, v);
        PlayerPrefs.Save();
    }

    private void Apply(float master, float music, float sfx)
    {
        SetVolume(MasterParam, master);
        SetVolume(MusicParam, music);
        SetVolume(SFXParam, sfx);
    }

    private void SetVolume(string param, float linear01)
    {
        if (_mixer == null) return;
        float db = linear01 <= 0.0001f
            ? MinDB
            : Mathf.Lerp(MinDB, MaxDB, Mathf.Log10(Mathf.Clamp(linear01, 0.0001f, 1f)) / 4f);
        _mixer.SetFloat(param, db);
    }
}