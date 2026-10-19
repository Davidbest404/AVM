using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Процедурный звук осциллографа: игрок слышит синусоиду, которую настраивает.
/// Частота звука пропорциональна частоте волны, громкость — амплитуде.
/// Чистый синус генерируется через OnAudioFilterRead.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class OscilloscopeAudio : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private OscilloscopeSystem _system;
    [SerializeField] private AudioMixerGroup _mixerGroup;

    [Header("Mapping")]
    [Tooltip("Множитель частоты волны → Гц. При baseFreq=4 и множителе 55 → 220 Гц.")]
    [SerializeField] private float _frequencyToHz = 55f;
    [Tooltip("Множитель амплитуды → громкость.")]
    [SerializeField] private float _amplitudeToVolume = 0.4f;
    [Range(0f, 1f)][SerializeField] private float _maxVolume = 0.15f;
    [Tooltip("Минимальная частота звука (Гц).")]
    [SerializeField] private float _minHz = 40f;
    [Tooltip("Максимальная частота звука (Гц).")]
    [SerializeField] private float _maxHz = 2000f;

    [Header("Smoothing")]
    [SerializeField] private float _smoothSpeed = 8f;

    private AudioSource _source;
    private float _currentHz;
    private float _currentVol;

    // Audio-thread state
    private double _phase;
    private int _sampleRate = 48000;

    private void Awake()
    {
        _source = GetComponent<AudioSource>();
        _source.playOnAwake = false;
        _source.loop = true;
        _source.spatialBlend = 0f;
        _source.outputAudioMixerGroup = _mixerGroup;

        // Создаём тихий клип на 48 кГц
        int sampleRate = 48000;
        var silent = AudioClip.Create("osc_silent", sampleRate, 1, sampleRate, false);
        var buf = new float[sampleRate];
        silent.SetData(buf, 0);
        _source.clip = silent;
        _source.Play();

        // Запоминаем частоту из клипа — это работает во всех версиях Unity
        _sampleRate = silent.frequency;
    }

    private void Update()
    {
        if (_system == null) return;

        float targetHz = Mathf.Clamp(_system.CurrentFrequency * _frequencyToHz, _minHz, _maxHz);
        float targetVol = Mathf.Clamp01(_system.CurrentAmplitude * _amplitudeToVolume) * _maxVolume;

        float dt = Time.deltaTime * _smoothSpeed;
        _currentHz = Mathf.Lerp(_currentHz, targetHz, dt);
        _currentVol = Mathf.Lerp(_currentVol, targetVol, dt);
    }

    // Runs on audio thread — никаких Unity API здесь!
    private void OnAudioFilterRead(float[] data, int channels)
    {
        if (_currentVol < 0.0001f) return;

        double increment = _currentHz * 2.0 * System.Math.PI / _sampleRate;

        for (int i = 0; i < data.Length; i += channels)
        {
            float sample = (float)System.Math.Sin(_phase) * _currentVol;
            _phase += increment;
            if (_phase > System.Math.PI * 2.0) _phase -= System.Math.PI * 2.0;

            for (int c = 0; c < channels; c++)
                data[i + c] += sample;
        }
    }
}