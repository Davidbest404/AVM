using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Звук ленты: мотор (loop), перемотка (loop), зажевание (one-shot).
/// Громкость включается мгновенно при смене состояния (мотор ↔ перемотка),
/// чтобы звук отмотки слышался сразу по нажатию.
/// </summary>
public class TapeAudio : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TapeSystem _system;
    [SerializeField] private AudioMixerGroup _mixerGroup;

    [Header("Clips")]
    [SerializeField] private AudioClip _motorLoop;
    [SerializeField] private AudioClip _rewindLoop;
    [SerializeField] private AudioClip _jamOneShot;

    [Header("Volume (0..1)")]
    [Range(0f, 1f)][SerializeField] private float _motorVolume = 0.7f;
    [Range(0f, 1f)][SerializeField] private float _rewindVolume = 0.8f;
    [Range(0f, 1f)][SerializeField] private float _jamVolume = 0.8f;

    [Header("Pitch / Speed mapping")]
    [SerializeField] private float _referenceSpeed = 90f;
    [SerializeField] private float _basePitch = 1f;
    [SerializeField] private float _pitchPer100Units = 0.3f;
    [SerializeField] private float _minPitch = 0.3f;
    [SerializeField] private float _maxPitch = 4f;

    [Header("Smoothing (только для питча)")]
    [SerializeField] private float _pitchSmoothSpeed = 8f;

    [Header("Debug")]
    [SerializeField] private bool _debugLog = false;

    private AudioSource _motorSource;
    private AudioSource _rewindSource;
    private AudioSource _oneShotSource;

    private float _smoothPitchMotor = 1f;
    private float _smoothPitchRewind = 1f;

    private TapeState _lastState = TapeState.Working;
    private int _lastDirection = 0; // 1 = motor, -1 = rewind, 0 = стоп

    private void Awake()
    {
        if (_system == null) _system = GetComponent<TapeSystem>();

        _motorSource = CreateLoopSource("Motor", _motorLoop);
        _rewindSource = CreateLoopSource("Rewind", _rewindLoop);
        _oneShotSource = CreateOneShotSource("TapeOneShot");
    }

    private void Start()
    {
        if (_system == null)
            Debug.LogError("[TapeAudio] _system не назначен и не найден!");
        if (_motorLoop == null)
            Debug.LogWarning("[TapeAudio] _motorLoop не назначен — мотор будет молчать.");
        if (_rewindLoop == null)
            Debug.LogWarning("[TapeAudio] _rewindLoop не назначен — перемотка будет молчать. ← главная причина!");
        if (_jamOneShot == null)
            Debug.LogWarning("[TapeAudio] _jamOneShot не назначен — зажевание без звука.");

        if (_system != null) _lastState = _system.State;
    }

    private AudioSource CreateLoopSource(string name, AudioClip clip)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.loop = true;
        src.playOnAwake = false;
        src.spatialBlend = 0f;
        src.volume = 0f;
        if (_mixerGroup != null) src.outputAudioMixerGroup = _mixerGroup;
        if (clip != null) src.Play();
        return src;
    }

    private AudioSource CreateOneShotSource(string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 0f;
        if (_mixerGroup != null) src.outputAudioMixerGroup = _mixerGroup;
        return src;
    }

    private void Update()
    {
        if (_system == null) return;

        float speed = _system.CurrentSpinSpeed;

        // Определяем направление: 1 = вперёд (мотор), -1 = назад (перемотка), 0 = стоп
        int direction = 0;
        if (speed > 0.5f) direction = 1;
        else if (speed < -0.5f) direction = -1;

        // Мгновенная смена громкости при переходе между состояниями
        if (direction != _lastDirection)
        {
            if (_debugLog)
                Debug.Log($"[TapeAudio] Направление: {_lastDirection} → {direction} (speed={speed:F0})");

            if (_motorSource != null)
                _motorSource.volume = direction == 1 ? _motorVolume : 0f;
            if (_rewindSource != null)
                _rewindSource.volume = direction == -1 ? _rewindVolume : 0f;

            // Гарантируем, что source играет (на случай, если clip появился позже)
            EnsurePlaying(_motorSource, _motorLoop);
            EnsurePlaying(_rewindSource, _rewindLoop);

            _lastDirection = direction;
        }

        // Питч плавно — чтобы при перемотке не было "кликов"
        if (direction != 0)
        {
            float absSpeed = Mathf.Abs(speed);
            float pitchOffset = (absSpeed - _referenceSpeed) / 100f * _pitchPer100Units;
            float targetPitch = Mathf.Clamp(_basePitch + pitchOffset, _minPitch, _maxPitch);

            float dt = Time.deltaTime * _pitchSmoothSpeed;
            if (direction == 1)
            {
                _smoothPitchMotor = Mathf.Lerp(_smoothPitchMotor, targetPitch, dt);
                if (_motorSource != null) _motorSource.pitch = _smoothPitchMotor;
            }
            else
            {
                _smoothPitchRewind = Mathf.Lerp(_smoothPitchRewind, targetPitch, dt);
                if (_rewindSource != null) _rewindSource.pitch = _smoothPitchRewind;
            }
        }

        // Зажевание
        if (_system.State == TapeState.Jammed && _lastState != TapeState.Jammed)
            PlayJam();

        _lastState = _system.State;
    }

    private void EnsurePlaying(AudioSource src, AudioClip clip)
    {
        if (src == null || clip == null) return;
        if (src.clip == null) src.clip = clip;
        if (!src.isPlaying) src.Play();
    }

    private void PlayJam()
    {
        if (_jamOneShot == null || _oneShotSource == null) return;
        _oneShotSource.PlayOneShot(_jamOneShot, _jamVolume);
    }
}