using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Звук осциллографа, совместимый с WebGL.
/// Генерирует базовый синус-клип и управляет частотой (pitch) и амплитудой (volume)
/// через AudioSource. Не использует OnAudioFilterRead.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class OscilloscopeAudio : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private OscilloscopeSystem _system;
    [SerializeField] private AudioMixerGroup _mixerGroup;

    [Header("Mapping")]
    [Tooltip("Базовая частота клипа в Гц. Обычно 440 (нота Ля).")]
    [SerializeField] private float _baseClipFrequency = 440f;
    [Tooltip("Множитель частоты волны для расчета pitch. " +
             "Например, при baseFreq=4 и множителе 55 целевая частота будет 220 Гц, " +
             "а pitch относительно 440 Гц составит 0.5.")]
    [SerializeField] private float _frequencyToHz = 55f;
    [Tooltip("Множитель амплитуды для расчета громкости.")]
    [SerializeField] private float _amplitudeToVolume = 0.4f;
    [Range(0f, 1f)][SerializeField] private float _maxVolume = 0.15f;

    [Header("Pitch Limits")]
    [SerializeField] private float _minPitch = 0.1f;
    [SerializeField] private float _maxPitch = 4f;

    [Header("Smoothing")]
    [SerializeField] private float _smoothSpeed = 8f;

    private AudioSource _source;
    private AudioClip _generatedClip;
    private float _currentPitch = 1f;
    private float _currentVolume;

    private void Awake()
    {
        _source = GetComponent<AudioSource>();
        _source.playOnAwake = false;
        _source.loop = true;
        _source.spatialBlend = 0f;
        if (_mixerGroup != null) _source.outputAudioMixerGroup = _mixerGroup;

        // Генерируем базовый синус-клип
        _generatedClip = GenerateSineClip("OscilloSine", _baseClipFrequency, 1f, 1f);
        _source.clip = _generatedClip;
    }

    private void Start()
    {
        // Проверяем, что звук вообще может играть (важно для WebGL)
        if (GameManager.Instance != null && GameManager.Instance.IsPaused) return;
        _source.Play();
    }

    private void Update()
    {
        if (_system == null || _source == null) return;

        // 1. Вычисляем целевую частоту в Гц на основе параметров волны
        float targetHz = _system.CurrentFrequency * _frequencyToHz;

        // 2. Переводим Гц в pitch относительно базовой частоты клипа
        float targetPitch = Mathf.Clamp(targetHz / _baseClipFrequency, _minPitch, _maxPitch);

        // 3. Вычисляем целевую громкость на основе амплитуды
        float targetVolume = Mathf.Clamp01(_system.CurrentAmplitude * _amplitudeToVolume) * _maxVolume;

        // 4. Плавно сглаживаем значения
        float dt = Time.deltaTime * _smoothSpeed;
        _currentPitch = Mathf.Lerp(_currentPitch, targetPitch, dt);
        _currentVolume = Mathf.Lerp(_currentVolume, targetVolume, dt);

        _source.pitch = _currentPitch;
        _source.volume = _currentVolume;
    }

    /// <summary>
    /// Генерирует AudioClip с синусоидой.
    /// </summary>
    private AudioClip GenerateSineClip(string name, float frequency, float amplitude, float duration)
    {
        int sampleRate = 44100; // Стандартная частота для WebGL
        int sampleCount = Mathf.RoundToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * amplitude;
        }

        // В WebGL streaming должен быть false, и все данные должны быть загружены сразу
        AudioClip clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private void OnDestroy()
    {
        if (_generatedClip != null) Destroy(_generatedClip);
    }
}