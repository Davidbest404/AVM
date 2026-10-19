using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// «вук вентил€тора: вращение (loop) + постукивание (one-shot).
/// √ромкость и питч вращени€ пропорциональны скорости лопастей.
/// </summary>
public class FanAudio : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CoolingSystem _system;
    [SerializeField] private AudioMixerGroup _mixerGroup;

    [Header("Clips")]
    [SerializeField] private AudioClip _spinLoop;
    [SerializeField] private AudioClip _knockOneShot;

    [Header("Spin Loop")]
    [Range(0f, 1f)][SerializeField] private float _spinMaxVolume = 0.5f;
    [Tooltip("—корость вращени€ (ед/сек), при которой питч = 1.")]
    [SerializeField] private float _referenceSpin = 720f;
    [Tooltip("ѕитч при максимальной скорости.")]
    [SerializeField] private float _maxPitch = 2f;
    [Tooltip("ћинимальный питч (не даЄт звуку уйти в инфразвук).")]
    [SerializeField] private float _minPitch = 0.5f;

    [Header("Knock")]
    [Range(0f, 1f)][SerializeField] private float _knockVolume = 0.7f;

    [Header("Smoothing")]
    [SerializeField] private float _smoothSpeed = 5f;

    private AudioSource _spinSource;
    private AudioSource _oneShotSource;
    private float _smoothVolume;
    private float _smoothPitch = 1f;

    private void Awake()
    {
        _spinSource = CreateLoopSource("FanSpin", _spinLoop);
        _oneShotSource = CreateOneShotSource("FanKnock");
        if (_knockOneShot == null)
            Debug.LogWarning("[FanAudio] _knockOneShot не назначен Ч стук будет без звука.");
        if (_spinLoop == null)
            Debug.LogWarning("[FanAudio] _spinLoop не назначен Ч вращение будет без звука.");
    }

    private AudioSource CreateLoopSource(string n, AudioClip clip)
    {
        var go = new GameObject(n);
        go.transform.SetParent(transform, false);
        var src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.loop = true;
        src.playOnAwake = false;
        src.spatialBlend = 0f;
        src.outputAudioMixerGroup = _mixerGroup;
        if (clip != null) src.Play();
        return src;
    }

    private AudioSource CreateOneShotSource(string n)
    {
        var go = new GameObject(n);
        go.transform.SetParent(transform, false);
        var src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 0f;
        src.outputAudioMixerGroup = _mixerGroup;
        return src;
    }

    private void Update()
    {
        if (_system == null) return;

        float spin = Mathf.Abs(_system.CurrentSpin);
        float spinNorm = Mathf.Clamp01(spin / _referenceSpin);

        float targetVol = spinNorm * _spinMaxVolume;
        float targetPitch = Mathf.Lerp(_minPitch, _maxPitch, spinNorm);

        float dt = Time.deltaTime * _smoothSpeed;
        _smoothVolume = Mathf.Lerp(_smoothVolume, targetVol, dt);
        _smoothPitch = Mathf.Lerp(_smoothPitch, targetPitch, dt);

        _spinSource.volume = _smoothVolume;
        _spinSource.pitch = _smoothPitch;
    }

    public void PlayKnock()
    {
        if (_knockOneShot != null && _oneShotSource != null)
            _oneShotSource.PlayOneShot(_knockOneShot, _knockVolume);
    }
}