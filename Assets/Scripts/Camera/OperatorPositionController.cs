using System;
using System.Collections.Generic;
using UnityEngine;

public class OperatorPositionController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform _cameraTransform;
    [SerializeField] private CursorPeekCamera _cursorPeek;

    [Header("Positions")]
    [SerializeField] private Transform _screenPoint;
    [SerializeField] private Transform _lampsPoint;
    [SerializeField] private Transform _tapePoint;
    [SerializeField] private Transform _fanPoint;

    [Header("Look Target")]
    [SerializeField] private Transform _lookTargetOverride;

    [Header("Transition")]
    [SerializeField] private float _moveDuration = 0.8f;
    [SerializeField] private AnimationCurve _moveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Peek Integration")]
    [SerializeField] private bool _usePeekOnArrival = true;

    [Header("Debug")]
    [SerializeField] private bool _logTransitions = false;

    public OperatorPosition CurrentPosition { get; private set; }
    public bool IsMoving { get; private set; }

    public event Action<OperatorPosition> OnPositionChanged;
    public event Action<OperatorPosition> OnTransitionStarted;

    private Vector3 _fromPos;
    private Quaternion _fromRot;
    private Vector3 _toPos;
    private Quaternion _toRot;
    private float _moveTimer;

    private readonly Dictionary<OperatorPosition, Transform> _points = new();
    private readonly Dictionary<OperatorPosition, List<OperatorPosition>> _allowed = new();

    private void Awake()
    {
        _points[OperatorPosition.Screen] = _screenPoint;
        _points[OperatorPosition.Lamps] = _lampsPoint;
        _points[OperatorPosition.Tape] = _tapePoint;
        _points[OperatorPosition.Fan] = _fanPoint;

        _allowed[OperatorPosition.Screen] = new List<OperatorPosition>
            { OperatorPosition.Lamps, OperatorPosition.Fan, OperatorPosition.Tape };
        _allowed[OperatorPosition.Lamps] = new List<OperatorPosition> { OperatorPosition.Screen };
        _allowed[OperatorPosition.Tape] = new List<OperatorPosition> { OperatorPosition.Screen };
        _allowed[OperatorPosition.Fan] = new List<OperatorPosition> { OperatorPosition.Screen };

        CurrentPosition = OperatorPosition.Screen;
        SnapToCurrent();
    }

    private void Start() { OnPositionChanged?.Invoke(CurrentPosition); }

    private void Update()
    {
        if (IsMoving)
        {
            _moveTimer += Time.deltaTime;
            float t = _moveCurve.Evaluate(Mathf.Clamp01(_moveTimer / _moveDuration));
            _cameraTransform.position = Vector3.Lerp(_fromPos, _toPos, t);
            _cameraTransform.rotation = Quaternion.Slerp(_fromRot, _toRot, t);
            if (t >= 1f) { IsMoving = false; FinishTransition(); }
            return;
        }

        if (IsInputBlocked()) return;

        if (Input.GetKeyDown(KeyCode.A)) TryMoveByKey(KeyCode.A);
        if (Input.GetKeyDown(KeyCode.D)) TryMoveByKey(KeyCode.D);
        if (Input.GetKeyDown(KeyCode.W)) TryMoveByKey(KeyCode.W);
        if (Input.GetKeyDown(KeyCode.S)) TryMoveByKey(KeyCode.S);
    }

    public void OnDirectionalButtonPressed(KeyCode key)
    {
        if (IsMoving || IsInputBlocked()) return;
        TryMoveByKey(key);
    }

    /// <summary>Доступно ли направление из текущей позиции?</summary>
    public bool IsDirectionAvailable(KeyCode key)
    {
        if (IsMoving) return false;
        if (IsInputBlocked()) return false;
        return GetTargetForInput(CurrentPosition, key).HasValue;
    }

    private bool IsInputBlocked()
    {
        return GameManager.Instance != null && GameManager.Instance.IsInputBlocked();
    }

    private void TryMoveByKey(KeyCode key)
    {
        OperatorPosition? target = GetTargetForInput(CurrentPosition, key);
        if (target.HasValue) MoveTo(target.Value);
    }

    private OperatorPosition? GetTargetForInput(OperatorPosition from, KeyCode key)
    {
        if (from == OperatorPosition.Screen)
        {
            switch (key)
            {
                case KeyCode.A: return OperatorPosition.Lamps;
                case KeyCode.S: return OperatorPosition.Fan;
                case KeyCode.D: return OperatorPosition.Tape;
            }
        }
        if (from == OperatorPosition.Lamps && key == KeyCode.D) return OperatorPosition.Screen;
        if (from == OperatorPosition.Tape && key == KeyCode.A) return OperatorPosition.Screen;
        if (from == OperatorPosition.Fan && key == KeyCode.W) return OperatorPosition.Screen;
        return null;
    }

    private Quaternion ComputeTargetRotation(OperatorPosition target, Vector3 position)
    {
        if (_lookTargetOverride != null)
        {
            Vector3 dir = _lookTargetOverride.position - position;
            if (dir.sqrMagnitude > 0.0001f)
                return Quaternion.LookRotation(dir.normalized, Vector3.up);
        }
        return _points[target].rotation;
    }

    private void MoveTo(OperatorPosition target)
    {
        if (!_allowed[CurrentPosition].Contains(target)) return;
        if (target == CurrentPosition) return;

        CurrentPosition = target;
        IsMoving = true;
        _moveTimer = 0f;

        _fromPos = _cameraTransform.position;
        _fromRot = _cameraTransform.rotation;
        _toPos = _points[target].position;
        _toRot = ComputeTargetRotation(target, _toPos);

        if (_cursorPeek != null) _cursorPeek.SetEnabled(false);

        if (_logTransitions)
            Debug.Log($"[OperatorPosition] MoveTo {target}  toRot={_toRot.eulerAngles}");

        OnTransitionStarted?.Invoke(target);
    }

    private void FinishTransition()
    {
        if (_cursorPeek != null)
        {
            _cursorPeek.SetBaseRotation(_cameraTransform.rotation);
            _cursorPeek.SetEnabled(_usePeekOnArrival);
        }
        OnPositionChanged?.Invoke(CurrentPosition);
    }

    private void SnapToCurrent()
    {
        if (_cameraTransform == null || _points[CurrentPosition] == null)
        {
            Debug.LogError("[OperatorPositionController] Не назначены cameraTransform или точки позиций.");
            return;
        }
        Transform p = _points[CurrentPosition];
        _cameraTransform.position = p.position;
        _cameraTransform.rotation = ComputeTargetRotation(CurrentPosition, p.position);

        if (_cursorPeek != null)
        {
            _cursorPeek.SetBaseRotation(_cameraTransform.rotation);
            _cursorPeek.SetEnabled(_usePeekOnArrival);
        }
    }
}