using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Лампа АВМ (Bulb + Screw как единый объект). Умеет выкручиваться, падать и удаляться.
/// Принадлежит сокету (LampSocket), пока не выкручена.
/// </summary>
public class Lamp : MonoBehaviour
{
    [Header("Visuals")]
    [SerializeField] private Renderer _bulbRenderer;
    [SerializeField] private Collider _collider;

    [Header("Colors (URP)")]
    [SerializeField] private Color _workingColor = new Color(0.2f, 1f, 0.3f);
    [SerializeField] private Color _damagedColor = new Color(1f, 0.15f, 0.1f);
    [SerializeField] private Color _faultyColor = new Color(0.05f, 0.05f, 0.05f);
    [Range(0f, 1f)][SerializeField] private float _damagedBlinkSpeed = 3f;

    [Header("Damage")]
    [Tooltip("Сколько секунд Damaged → Faulty.")]
    [SerializeField] private float _damagedToFaultyTime = 15f;

    [Header("Unwind Visuals")]
    [Tooltip("Высота подъёма при полном выкручивании (юниты).")]
    [SerializeField] private float _unwindLiftHeight = 0.15f;

    [Header("Fall")]
    [SerializeField] private float _fallDuration = 1.5f;
    [SerializeField] private float _fallDistance = 2f;
    [SerializeField] private float _fallRotation = 720f;

    // ---- Публичное ----
    public LampState State { get; private set; } = LampState.Working;
    public LampSocket Socket { get; private set; }
    public bool IsInstalled => Socket != null;

    public event Action<Lamp, LampState> OnStateChanged;

    // ---- Внутреннее ----
    private float _damagedTimer;
    private Transform _originalParent;
    private MaterialPropertyBlock _mpb;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private void Awake()
    {
        _mpb = new MaterialPropertyBlock();
        _originalParent = transform.parent;
    }

    private void Update()
    {
        if (State == LampState.Damaged)
        {
            _damagedTimer += Time.deltaTime;
            float blink = 0.5f + 0.5f * Mathf.Sin(Time.time * _damagedBlinkSpeed * Mathf.PI * 2f);
            SetBulbColor(Color.Lerp(_damagedColor * 0.3f, _damagedColor, blink));

            if (_damagedTimer >= _damagedToFaultyTime)
                SetState(LampState.Faulty);
        }
    }

    // ---- Состояния ----

    public void SetState(LampState s)
    {
        if (State == s) return;
        State = s;
        _damagedTimer = 0f;
        ApplyColor();
        OnStateChanged?.Invoke(this, s);
    }

    // ---- Установка ----

    /// <summary>
    /// Установка в сокет. Сохраняет мировой масштаб лампы —
    /// чтобы не «уменьшалась», если у сокета другой scale.
    /// </summary>
    public void InstallTo(LampSocket socket)
    {
        Socket = socket;

        Vector3 worldScaleBefore = transform.lossyScale;

        transform.SetParent(socket.InstallPoint, worldPositionStays: false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;

        // Компенсируем масштаб родителя, чтобы мировой scale лампы остался прежним
        Vector3 parentWS = transform.parent.lossyScale;
        transform.localScale = new Vector3(
            parentWS.x != 0 ? worldScaleBefore.x / parentWS.x : 1f,
            parentWS.y != 0 ? worldScaleBefore.y / parentWS.y : 1f,
            parentWS.z != 0 ? worldScaleBefore.z / parentWS.z : 1f
        );

        if (_collider != null) _collider.enabled = true;
        if (State != LampState.Working) SetState(LampState.Working);
    }

    public void DetachFromSocket()
    {
        if (Socket != null)
        {
            Socket.ClearLamp();
            Socket = null;
        }
        // worldPositionStays: true сохраняет и позицию, и масштаб в мире
        transform.SetParent(_originalParent, worldPositionStays: true);
        if (_collider != null) _collider.enabled = false; // падающая лампа не кликабельна
    }

    // ---- Выкручивание ----

    /// <summary>progress 0..1 — насколько выкручена.</summary>
    public void SetUnwindProgress(float progress)
    {
        float y = progress * _unwindLiftHeight;
        transform.localPosition = new Vector3(0f, y, 0f);
        transform.localRotation = Quaternion.Euler(0f, progress * 360f, 0f);
    }

    public void ResetUnwindVisual()
    {
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
    }

    // ---- Падение ----

    public void StartFalling()
    {
        if (_collider != null) _collider.enabled = false;
        StartCoroutine(FallRoutine());
    }

    private IEnumerator FallRoutine()
    {
        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;
        Vector3 endPos = startPos + Vector3.down * _fallDistance;
        Quaternion endRot = startRot * Quaternion.Euler(_fallRotation, _fallRotation * 0.5f, 0f);

        float t = 0f;
        while (t < _fallDuration)
        {
            t += Time.deltaTime;
            float n = Mathf.Clamp01(t / _fallDuration);
            transform.position = Vector3.Lerp(startPos, endPos, n * n);
            transform.rotation = Quaternion.Slerp(startRot, endRot, n);

            float alpha = 1f - n * 0.9f;
            SetAlpha(_bulbRenderer, alpha);
            yield return null;
        }

        Destroy(gameObject);
    }

    // ---- Визуал ----

    private void ApplyColor()
    {
        switch (State)
        {
            case LampState.Working: SetBulbColor(_workingColor * 2f); break;
            case LampState.Damaged: SetBulbColor(_damagedColor * 2f); break;
            case LampState.Faulty: SetBulbColor(_faultyColor); break;
        }
    }

    private void SetBulbColor(Color c)
    {
        if (_bulbRenderer == null) return;
        _bulbRenderer.GetPropertyBlock(_mpb);
        _mpb.SetColor(BaseColorId, c);
        _mpb.SetColor(ColorId, c);
        _mpb.SetColor(EmissionColorId, c * 3f);
        _bulbRenderer.SetPropertyBlock(_mpb);
    }

    private void SetAlpha(Renderer rend, float a)
    {
        if (rend == null) return;
        rend.GetPropertyBlock(_mpb);
        Color c = Color.white;
        if (rend.sharedMaterial != null)
        {
            if (rend.sharedMaterial.HasProperty(BaseColorId)) c = rend.sharedMaterial.GetColor(BaseColorId);
            else if (rend.sharedMaterial.HasProperty(ColorId)) c = rend.sharedMaterial.GetColor(ColorId);
        }
        c.a = a;
        _mpb.SetColor(BaseColorId, c);
        _mpb.SetColor(ColorId, c);
        rend.SetPropertyBlock(_mpb);
    }
}