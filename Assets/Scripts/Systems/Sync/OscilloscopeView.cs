using UnityEngine;

/// <summary>
/// ќбновл€ет материал CRT-экрана: реальна€ волна (по параметрам системы)
/// и призрак целевой волны. ÷вет реальной волны зависит от качества.
/// </summary>
[RequireComponent(typeof(Renderer))]
public class OscilloscopeView : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private OscilloscopeSystem _system;

    [Header("Colors")]
    [Tooltip("÷вет волны при идеальном качестве.")]
    [SerializeField] private Color _goodColor = new Color(0.3f, 1f, 0.4f);
    [Tooltip("÷вет волны при среднем качестве.")]
    [SerializeField] private Color _warningColor = new Color(1f, 0.85f, 0.2f);
    [Tooltip("÷вет волны при плохом качестве.")]
    [SerializeField] private Color _badColor = new Color(1f, 0.25f, 0.15f);
    [Tooltip("÷вет 'призрака' Ч целева€ волна.")]
    [SerializeField] private Color _ghostColor = new Color(0.2f, 0.4f, 0.2f);

    [Header("Smoothing")]
    [SerializeField] private float _lerpSpeed = 8f;

    // ---- ¬нутреннее ----
    private Renderer _renderer;
    private Material _mat;
    private float _smoothFreq;
    private float _smoothAmp;
    private Color _smoothColor;

    private static readonly int WaveFreqId = Shader.PropertyToID("_WaveFrequency");
    private static readonly int WaveAmpId = Shader.PropertyToID("_WaveAmplitude");
    private static readonly int BaseFreqId = Shader.PropertyToID("_BaseFrequency");
    private static readonly int BaseAmpId = Shader.PropertyToID("_BaseAmplitude");
    private static readonly int WaveColorId = Shader.PropertyToID("_WaveColor");
    private static readonly int GhostColorId = Shader.PropertyToID("_GhostColor");

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
        _mat = _renderer.material;
    }

    private void Start()
    {
        if (_system != null)
        {
            _smoothFreq = _system.CurrentFrequency;
            _smoothAmp = _system.CurrentAmplitude;
            _smoothColor = _goodColor;

            if (_mat != null)
            {
                _mat.SetFloat(BaseFreqId, _system.BaseFrequency);
                _mat.SetFloat(BaseAmpId, _system.BaseAmplitude);
                _mat.SetColor(GhostColorId, _ghostColor);
            }
        }
    }

    private void Update()
    {
        if (_system == null || _mat == null) return;

        // ѕлавно сглаживаем значени€, чтобы волна не дЄргалась от случайного шума
        _smoothFreq = Mathf.Lerp(_smoothFreq, _system.CurrentFrequency, Time.deltaTime * _lerpSpeed);
        _smoothAmp = Mathf.Lerp(_smoothAmp, _system.CurrentAmplitude, Time.deltaTime * _lerpSpeed);

        // ÷вет по качеству
        Color targetColor = GetColorForQuality(_system.Quality);
        _smoothColor = Color.Lerp(_smoothColor, targetColor, Time.deltaTime * _lerpSpeed);

        _mat.SetFloat(WaveFreqId, _smoothFreq);
        _mat.SetFloat(WaveAmpId, _smoothAmp);
        _mat.SetColor(WaveColorId, _smoothColor);
    }

    private Color GetColorForQuality(float q)
    {
        if (q >= 0.9f)
            return Color.Lerp(_warningColor, _goodColor, (q - 0.9f) / 0.1f);
        if (q >= 0.5f)
            return Color.Lerp(_badColor, _warningColor, (q - 0.5f) / 0.4f);
        return _badColor;
    }
}