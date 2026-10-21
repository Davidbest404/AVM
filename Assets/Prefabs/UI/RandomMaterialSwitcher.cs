using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Фоновый декоративный эффект: постоянно и случайно выбирает Renderer из списка,
/// случайный материал из списка и плавно перекрашивает цвета первого
/// в цвета второго. Не зависит ни от каких игровых систем.
/// Работает на Time.unscaledDeltaTime — идёт даже на паузе.
/// </summary>
public class RandomMaterialSwitcher : MonoBehaviour
{
    [Header("Objects")]
    [Tooltip("Рендереры, между которыми выбираем случайно.")]
    [SerializeField] private List<Renderer> _renderers = new();

    [Header("Materials")]
    [Tooltip("Материалы-«цели». У каждого рендерера цвета плавно уезжают в один из них.")]
    [SerializeField] private List<Material> _materials = new();

    [Header("Timing")]
    [Tooltip("Минимальная пауза между переходами (сек).")]
    [SerializeField] private float _minDelay = 2f;
    [Tooltip("Максимальная пауза между переходами (сек).")]
    [SerializeField] private float _maxDelay = 5f;
    [Tooltip("Сколько длится сам переход (сек).")]
    [SerializeField] private float _transitionDuration = 1.5f;

    [Header("Color Properties")]
    [Tooltip("Какие color-свойства перекрашивать. По умолчанию URP: _BaseColor, _EmissionColor.")]
    [SerializeField] private string[] _colorProperties = { "_BaseColor", "_EmissionColor" };

    [Header("Behavior")]
    [Tooltip("Не выбирать тот же материал, что уже стоит на рендерере.")]
    [SerializeField] private bool _avoidRepeat = true;

    [Header("Debug")]
    [SerializeField] private bool _debugLog = false;

    // ---- Внутреннее ----
    private readonly Dictionary<Renderer, Material> _instances = new();
    private readonly Dictionary<Renderer, Material> _lastTarget = new();
    private readonly HashSet<Renderer> _busy = new();

    private void Awake()
    {
        // Каждому рендереру — своя копия материала, чтобы не делить цвета
        foreach (var r in _renderers)
        {
            if (r == null) continue;
            Material inst = r.material; // Unity сам клонирует из shared
            _instances[r] = inst;
        }
    }

    private void Start()
    {
        if (_renderers.Count == 0 || _materials.Count == 0)
        {
            Debug.LogWarning("[RandomMaterialSwitcher] Один из списков пуст — эффект отключён.");
            enabled = false;
            return;
        }

        StartCoroutine(Loop());
    }

    private IEnumerator Loop()
    {
        while (true)
        {
            yield return new WaitForSecondsRealtime(Random.Range(_minDelay, _maxDelay));

            // Собираем свободные рендереры
            var available = new List<Renderer>();
            foreach (var r in _renderers)
                if (r != null && !_busy.Contains(r)) available.Add(r);

            if (available.Count == 0) continue;

            var renderer = available[Random.Range(0, available.Count)];
            var target = PickTargetMaterial(renderer);

            if (target != null)
                StartCoroutine(Transition(renderer, target));
        }
    }

    private Material PickTargetMaterial(Renderer r)
    {
        if (_materials.Count == 0) return null;
        if (_materials.Count == 1 || !_avoidRepeat) return _materials[Random.Range(0, _materials.Count)];

        Material last = _lastTarget.TryGetValue(r, out var m) ? m : null;
        for (int i = 0; i < 8; i++) // пара попыток — почти всегда срабатывает с первого раза
        {
            var candidate = _materials[Random.Range(0, _materials.Count)];
            if (candidate != last) return candidate;
        }
        return _materials[Random.Range(0, _materials.Count)];
    }

    private IEnumerator Transition(Renderer r, Material target)
    {
        _busy.Add(r);

        if (!_instances.TryGetValue(r, out var mat))
        {
            _busy.Remove(r);
            yield break;
        }

        int n = _colorProperties.Length;
        var fromColors = new Color[n];
        var toColors = new Color[n];
        for (int i = 0; i < n; i++)
        {
            string p = _colorProperties[i];
            if (mat.HasProperty(p)) fromColors[i] = mat.GetColor(p);
            if (target.HasProperty(p)) toColors[i] = target.GetColor(p);
        }

        float t = 0f;
        while (t < _transitionDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / _transitionDuration);
            for (int i = 0; i < n; i++)
            {
                string p = _colorProperties[i];
                if (mat.HasProperty(p))
                    mat.SetColor(p, Color.Lerp(fromColors[i], toColors[i], k));
            }
            yield return null;
        }

        _lastTarget[r] = target;
        _busy.Remove(r);

        if (_debugLog)
            Debug.Log($"[RandomMaterialSwitcher] {r.name} → {target.name}");
    }
}