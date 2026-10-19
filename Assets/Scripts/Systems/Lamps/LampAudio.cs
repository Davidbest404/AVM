using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Звук одной лампы:
/// — работа (loop, пока Working)
/// — мигание (loop, пока Damaged)
/// — хлопок при поломке (one-shot)
/// — выкручивание (loop, пока идёт выкрутка)
/// — вкручивание (one-shot при установке)
/// </summary>
[RequireComponent(typeof(Lamp))]
public class LampAudio : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Lamp _lamp;
    [SerializeField] private AudioMixerGroup _mixerGroup;

    [Header("Clips")]
    [SerializeField] private AudioClip _workingLoop;
    [SerializeField] private AudioClip _blinkLoop;
    [SerializeField] private AudioClip _popOneShot;
    [SerializeField] private AudioClip _unscrewLoop;
    [SerializeField] private AudioClip _screwInOneShot;

    [Header("Volume (0..1)")]
    [Range(0f, 1f)][SerializeField] private float _workingVolume = 0.3f;
    [Range(0f, 1f)][SerializeField] private float _blinkVolume = 0.4f;
    [Range(0f, 1f)][SerializeField] private float _popVolume = 0.8f;
    [Range(0f, 1f)][SerializeField] private float _unscrewVolume = 0.5f;
    [Range(0f, 1f)][SerializeField] private float _screwInVolume = 0.7f;

    [Header("Smoothing")]
    [SerializeField] private float _smoothSpeed = 8f;

    private AudioSource _workingSource;
    private AudioSource _blinkSource;
    private AudioSource _unscrewSource;
    private AudioSource _oneShotSource;

    private float _smoothWorking;
    private float _smoothBlink;
    private float _smoothUnscrew;

    private LampState _lastState;
    private bool _isUnwinding;
    private bool _wasInstalled;

    private void Awake()
    {
        if (_lamp == null) _lamp = GetComponent<Lamp>();

        _workingSource = CreateLoopSource("LampWorking", _workingLoop);
        _blinkSource = CreateLoopSource("LampBlink", _blinkLoop);
        _unscrewSource = CreateLoopSource("LampUnscrew", _unscrewLoop);
        _oneShotSource = CreateOneShotSource("LampOneShot");
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

    private void Start()
    {
        _lastState = _lamp.State;
        _wasInstalled = _lamp.IsInstalled;
    }

    // ---------- Внешний API (для LampInteractionController) ----------
    public void SetUnwinding(bool unwinding) => _isUnwinding = unwinding;

    // ----------

    private void Update()
    {
        bool installed = _lamp.IsInstalled;
        LampState state = _lamp.State;

        float targetWorking = 0f;
        float targetBlink = 0f;
        float targetUnscrew = 0f;

        if (installed)
        {
            if (state == LampState.Working) targetWorking = _workingVolume;
            else if (state == LampState.Damaged) targetBlink = _blinkVolume;
            // Faulty — тишина
        }

        if (_isUnwinding) targetUnscrew = _unscrewVolume;

        float dt = Time.deltaTime * _smoothSpeed;
        _smoothWorking = Mathf.Lerp(_smoothWorking, targetWorking, dt);
        _smoothBlink = Mathf.Lerp(_smoothBlink, targetBlink, dt);
        _smoothUnscrew = Mathf.Lerp(_smoothUnscrew, targetUnscrew, dt);

        _workingSource.volume = _smoothWorking;
        _blinkSource.volume = _smoothBlink;
        _unscrewSource.volume = _smoothUnscrew;

        // Хлопок при переходе Working → (Damaged | Faulty)
        bool justBroke = _lastState == LampState.Working && state != LampState.Working;
        if (justBroke) PlayPop();

        // Вкручивание при переходе not-installed → installed
        if (installed && !_wasInstalled) PlayScrewIn();

        _lastState = state;
        _wasInstalled = installed;
    }

    private void PlayPop()
    {
        if (_popOneShot != null && _oneShotSource != null)
            _oneShotSource.PlayOneShot(_popOneShot, _popVolume);
    }

    public void PlayScrewIn()
    {
        if (_screwInOneShot != null && _oneShotSource != null)
            _oneShotSource.PlayOneShot(_screwInOneShot, _screwInVolume);
    }
}