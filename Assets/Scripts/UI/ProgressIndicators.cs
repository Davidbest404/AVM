using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Прогресс-индикаторы: равномерно распределяют 0-100% прогресса между N объектами.
/// 2 индикатора = каждые 50%, 5 = каждые 20%, 10 = каждые 10%, 20 = каждые 5%.
/// Меняет материал в зависимости от того, «пройден» ли порог.
/// </summary>
public class ProgressIndicators : MonoBehaviour
{
    [Header("References")]
    [Tooltip("GameManager. Если пусто — найдёт автоматически.")]
    [SerializeField] private GameManager _gameManager;

    [Header("Indicators")]
    [Tooltip("Рендереры индикаторов (по порядку). Первый загорается первым.")]
    [SerializeField] private List<Renderer> _indicators = new();

    [Header("Materials")]
    [Tooltip("Материал выключенного индикатора.")]
    [SerializeField] private Material _offMaterial;
    [Tooltip("Материал включённого индикатора.")]
    [SerializeField] private Material _onMaterial;

    [Header("Transition")]
    [Tooltip("Плавный переход материалов (только для _BaseColor / _EmissionColor).")]
    [SerializeField] private bool _smoothTransition = true;
    [SerializeField] private float _smoothDuration = 0.3f;

    [Header("Behavior")]
    [Tooltip("Если true — при 100% прогресса зажигаются все индикаторы.")]
    [SerializeField] private bool _allOnAtHundred = true;

    [Header("Debug")]
    [SerializeField] private bool _debugLog = false;

    // ---- Внутреннее ----
    private int _indicatorCount;
    private bool[] _isOn;
    private Material[] _instanceMats;
    private Coroutine[] _transitions;

    private void Awake()
    {
        if (_gameManager == null) _gameManager = GameManager.Instance;
    }

    private void Start()
    {
        if (_gameManager == null)
        {
            Debug.LogError("[ProgressIndicators] GameManager не найден.");
            enabled = false;
            return;
        }

        _indicatorCount = _indicators.Count;
        if (_indicatorCount == 0)
        {
            Debug.LogWarning("[ProgressIndicators] Список индикаторов пуст.");
            enabled = false;
            return;
        }

        _isOn = new bool[_indicatorCount];
        _instanceMats = new Material[_indicatorCount];
        _transitions = new Coroutine[_indicatorCount];

        // Каждому индикатору — свой экземпляр материала
        for (int i = 0; i < _indicatorCount; i++)
        {
            var r = _indicators[i];
            if (r == null) continue;
            _instanceMats[i] = r.material;
            // Стартовое состояние — выключен
            if (_offMaterial != null)
            {
                _instanceMats[i] = _offMaterial;
                r.material = _offMaterial;
            }
        }

        _gameManager.OnProgressChanged += HandleProgress;

        if (_debugLog)
            Debug.Log($"[ProgressIndicators] Инициализировано {_indicatorCount} индикаторов. " +
                      $"Порог каждого: {100f / _indicatorCount:F1}%.");
    }

    private void OnDestroy()
    {
        if (_gameManager != null)
            _gameManager.OnProgressChanged -= HandleProgress;
    }

    private void HandleProgress(float progress01to100)
    {
        // Для каждого индикатора: его «порог» = (i+1) * step
        float step = 100f / _indicatorCount;

        for (int i = 0; i < _indicatorCount; i++)
        {
            // Индикатор i зажигается, когда прогресс превышает (i+1)*step? Нет.
            // Индикатор 0 (первый) — 0-50%, индикатор 1 (второй) — 50-100%.
            // То есть i-й индикатор горит, если progress >= i * step (или >= (i+1)*step)?
            // Проще: индикатор горит, если progress >= (i+1)*step / _indicatorCount... 
            // Нет, давай так: индикатор горит когда прогресс прошёл порог i*step.
            // Тогда при 0% горит только нулевой (i=0, порог 0).
            // При 50% горят 0 и 1 (если 2 индикатора). 
            // Хочешь чтобы «0% = ничего»? Тогда порог = (i+1) * step.
            // Сделаю так: 0% = ничего не горит, 100% = всё горит.
            float threshold = (i + 1) * step;
            bool shouldBeOn = progress01to100 >= threshold;

            if (_allOnAtHundred && progress01to100 >= 100f - 0.01f)
                shouldBeOn = true;

            if (shouldBeOn != _isOn[i])
                SetIndicator(i, shouldBeOn);
        }
    }

    private void SetIndicator(int index, bool on)
    {
        if (index < 0 || index >= _indicatorCount) return;

        _isOn[index] = on;
        Material target = on ? _onMaterial : _offMaterial;

        if (target == null)
        {
            Debug.LogWarning($"[ProgressIndicators] Материал для {(on ? "ON" : "OFF")} не назначен.");
            return;
        }

        if (_transitions[index] != null)
            StopCoroutine(_transitions[index]);

        if (_smoothTransition)
            _transitions[index] = StartCoroutine(SmoothSwitch(index, target));
        else
        {
            _instanceMats[index] = target;
            _indicators[index].material = target;
        }

        if (_debugLog)
            Debug.Log($"[ProgressIndicators] #{index} → {(on ? "ON" : "OFF")}");
    }

    private System.Collections.IEnumerator SmoothSwitch(int index, Material target)
    {
        var r = _indicators[index];
        if (r == null) yield break;

        Material from = _instanceMats[index];
        if (from == null) { r.material = target; _instanceMats[index] = target; yield break; }

        // Клон-промежуточный, чтобы lerp-ить цвета
        Material mid = new Material(from);
        r.material = mid;

        float t = 0f;
        while (t < _smoothDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / _smoothDuration);

            LerpColor(mid, from, target, k, "_BaseColor");
            LerpColor(mid, from, target, k, "_Color");
            LerpColor(mid, from, target, k, "_EmissionColor");
            yield return null;
        }

        r.material = target;
        _instanceMats[index] = target;
        Destroy(mid);
        _transitions[index] = null;
    }

    private void LerpColor(Material result, Material a, Material b, float k, string prop)
    {
        if (!result.HasProperty(prop)) return;
        Color ca = (a != null && a.HasProperty(prop)) ? a.GetColor(prop) : result.GetColor(prop);
        Color cb = (b != null && b.HasProperty(prop)) ? b.GetColor(prop) : result.GetColor(prop);
        result.SetColor(prop, Color.Lerp(ca, cb, k));
    }
}