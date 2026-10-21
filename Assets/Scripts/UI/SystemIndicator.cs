using System.Collections;
using UnityEngine;

/// <summary>
/// Универсальный индикатор состояния системы: меняет материал Renderer'а
/// в зависимости от SystemState. Умеет переключать мгновенно или плавно.
///
/// Может работать в двух режимах:
///  1) Подписка на OnStateChanged системы (OnStateChanged event).
///  2) Пассивный опрос (для случая «хотя бы одна лампа / все лампы»).
/// </summary>
public class SystemIndicator : MonoBehaviour
{
    public enum Mode
    {
        Normal,        // По SystemState системы
        LampsAnyBroken,// Активен, если хотя бы одна лампа не Working
        LampsAllBroken // Активен, если ВСЕ лампы не Working
    }

    [Header("Target")]
    [Tooltip("Renderer, у которого меняется материал.")]
    [SerializeField] private Renderer _renderer;

    [Tooltip("Индекс материала в Renderer.materials. 0 — если один материал.")]
    [SerializeField] private int _materialIndex = 0;

    [Header("Mode")]
    [SerializeField] private Mode _mode = Mode.Normal;

    [Header("References (зависит от режима)")]
    [Tooltip("Для Mode.Normal — система с событием OnStateChanged (LampSystem, TapeSystem, CoolingSystem).")]
    [SerializeField] private MonoBehaviour _systemReference;

    [Tooltip("Для режимов LampsAnyBroken / LampsAllBroken — ссылка на LampSystem.")]
    [SerializeField] private LampSystem _lampSystem;

    [Header("Materials")]
    [Tooltip("Материал для Normal / Working.")]
    [SerializeField] private Material _normalMaterial;
    [Tooltip("Материал для Warning / Jammed.")]
    [SerializeField] private Material _warningMaterial;
    [Tooltip("Материал для Broken / Overheating / AllBroken.")]
    [SerializeField] private Material _brokenMaterial;
    [Tooltip("Материал для Repairing.")]
    [SerializeField] private Material _repairingMaterial;

    [Header("Transition")]
    [Tooltip("Плавный переход между материалами. Для ламп и вентилятора обычно false, для ленты — true.")]
    [SerializeField] private bool _smoothTransition = false;
    [Tooltip("Длительность плавного перехода (сек).")]
    [SerializeField] private float _smoothDuration = 0.4f;

    [Header("Debug")]
    [SerializeField] private bool _debugLog = false;

    // ---- Внутреннее ----
    private Material[] _materials;       // кэш renderer.materials
    private Material _currentMaterial;
    private Material _targetMaterial;
    private Coroutine _smoothCoroutine;

    private SystemState _currentState = SystemState.Normal;

    // ============================================================
    //  Инициализация
    // ============================================================

    private void Awake()
    {
        if (_renderer == null) _renderer = GetComponent<Renderer>();
        if (_renderer == null)
        {
            Debug.LogError($"[SystemIndicator {name}] Renderer не найден!");
            return;
        }

        _materials = _renderer.materials;
        if (_materialIndex >= _materials.Length)
        {
            Debug.LogError($"[SystemIndicator {name}] _materialIndex {_materialIndex} вне диапазона ({_materials.Length})");
            _materialIndex = 0;
        }

        _currentMaterial = _normalMaterial;
        _targetMaterial = _normalMaterial;
    }

    private void Start()
    {
        // Подписка (Normal mode)
        if (_mode == Mode.Normal)
        {
            if (_systemReference is IMachineSystem sys)
            {
                sys.OnStateChanged += HandleStateChanged;
                HandleStateChanged(GetCurrentState(sys));
            }
            else if (_systemReference != null)
            {
                Debug.LogWarning($"[SystemIndicator {name}] _systemReference не реализует IMachineSystem.");
            }
        }
    }

    private void OnDestroy()
    {
        if (_mode == Mode.Normal && _systemReference is IMachineSystem sys)
            sys.OnStateChanged -= HandleStateChanged;
    }

    private void Update()
    {
        // Опрос для ламповых режимов
        if (_mode == Mode.LampsAnyBroken || _mode == Mode.LampsAllBroken)
            PollLampsMode();
    }

    // ============================================================
    //  Normal mode — реакция на события
    // ============================================================

    private void HandleStateChanged(SystemState state)
    {
        _currentState = state;
        SetMaterialForState(state);
    }

    private void SetMaterialForState(SystemState state)
    {
        Material target = state switch
        {
            SystemState.Normal => _normalMaterial,
            SystemState.Warning => _warningMaterial,
            SystemState.Broken => _brokenMaterial,
            SystemState.Repairing => _repairingMaterial != null ? _repairingMaterial : _warningMaterial,
            _ => _normalMaterial
        };
        ApplyMaterial(target, _smoothTransition);
    }

    // ============================================================
    //  Lamps modes — опрос каждые N мс
    // ============================================================

    private void PollLampsMode()
    {
        if (_lampSystem == null) return;

        // Достаём доступ к сокетам (публичное поле?) — если нет, используем счёт по состоянию
        // В LampSystem добавим публичный метод, см. раздел 2.
        int total = _lampSystem.TotalLampsCount;
        int working = _lampSystem.WorkingLampsCount;
        int broken = total - working;

        bool trigger = _mode == Mode.LampsAnyBroken
            ? broken > 0
            : (total > 0 && working == 0);

        Material target = trigger
            ? (_mode == Mode.LampsAnyBroken ? _warningMaterial : _brokenMaterial)
            : _normalMaterial;

        if (target != _targetMaterial)
            ApplyMaterial(target, false);
    }

    // ============================================================
    //  Материалы
    // ============================================================

    private void ApplyMaterial(Material target, bool smooth)
    {
        if (target == null || _materials == null) return;
        if (_targetMaterial == target && !smooth) return;

        _targetMaterial = target;

        if (smooth)
        {
            if (_smoothCoroutine != null) StopCoroutine(_smoothCoroutine);
            _smoothCoroutine = StartCoroutine(SmoothSwitch(_materials[_materialIndex], target));
        }
        else
        {
            if (_smoothCoroutine != null) { StopCoroutine(_smoothCoroutine); _smoothCoroutine = null; }
            _materials[_materialIndex] = target;
            _renderer.materials = _materials; // присваиваем массив обратно — обязательно
        }

        if (_debugLog)
            Debug.Log($"[SystemIndicator {name}] → {target.name}");
    }

    private IEnumerator SmoothSwitch(Material from, Material to)
    {
        // Клонируем «промежуточный» материал, чтобы не менять ассеты
        Material mid = new Material(from);
        _materials[_materialIndex] = mid;
        _renderer.materials = _materials;

        float t = 0f;
        while (t < _smoothDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / _smoothDuration);
            LerpMaterial(mid, from, to, k);
            yield return null;
        }

        // В конце — присваиваем целевой (без клона)
        _materials[_materialIndex] = to;
        _renderer.materials = _materials;

        Destroy(mid);
        _smoothCoroutine = null;
    }

    /// <summary>
    /// Плавный переход между материалами по общим свойствам:
    /// _BaseColor, _Color, _EmissionColor.
    /// </summary>
    private void LerpMaterial(Material result, Material a, Material b, float k)
    {
        LerpColorProp(result, a, b, k, "_BaseColor");
        LerpColorProp(result, a, b, k, "_Color");
        LerpColorProp(result, a, b, k, "_EmissionColor");
    }

    private void LerpColorProp(Material result, Material a, Material b, float k, string prop)
    {
        if (!result.HasProperty(prop)) return;

        Color ca = a != null && a.HasProperty(prop) ? a.GetColor(prop) : result.GetColor(prop);
        Color cb = b != null && b.HasProperty(prop) ? b.GetColor(prop) : result.GetColor(prop);
        result.SetColor(prop, Color.Lerp(ca, cb, k));
    }

    private SystemState GetCurrentState(IMachineSystem sys)
    {
        if (sys == null) return SystemState.Normal;
        if (sys.IsBroken) return SystemState.Broken;
        return SystemState.Normal;
    }
}