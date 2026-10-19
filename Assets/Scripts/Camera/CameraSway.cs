using UnityEngine;

/// <summary>
/// ѕокачивание камеры Ч эффект Ђот первого лицаї: дыхание, горизонтальный дрейф, наклон головы.
/// –аботает ѕќ—Ћ≈ CursorPeekCamera (DefaultExecutionOrder 50) Ч накладывает поворот поверх peek,
/// не перезаписыва€ его. ¬о врем€ перелЄтов между позици€ми отключаетс€ автоматически.
/// </summary>
[DefaultExecutionOrder(50)]
public class CameraSway : MonoBehaviour
{
    [Header("References")]
    [Tooltip(" онтроллер позиций. Ќужен, чтобы отключать покачивание во врем€ перелЄтов.")]
    [SerializeField] private OperatorPositionController _positionController;

    [Header("Breathing (vertical bob)")]
    [Tooltip("јмплитуда вертикального дыхани€ (метры). 0.003Ц0.005 Ч заметно, но не мешает.")]
    [SerializeField] private float _breathAmplitudeY = 0.004f;
    [Tooltip("„астота вертикального дыхани€. 1.0Ц1.5 Ч как спокойное дыхание.")]
    [SerializeField] private float _breathSpeedY = 1.1f;

    [Header("Idle sway (horizontal)")]
    [Tooltip("јмплитуда горизонтального дрейфа (метры).")]
    [SerializeField] private float _swayAmplitudeX = 0.002f;
    [Tooltip("„астота горизонтального дрейфа.")]
    [SerializeField] private float _swaySpeedX = 0.4f;

    [Header("Head tilt (roll)")]
    [Tooltip("јмплитуда наклона головы (градусы). 0.3Ц0.6 Ч тонкий эффект.")]
    [SerializeField] private float _tiltAmplitude = 0.4f;
    [Tooltip("„астота наклона головы.")]
    [SerializeField] private float _tiltSpeed = 0.5f;

    [Header("Smoothing")]
    [Tooltip("—глаживание смещени€. Ѕольше Ч плавнее, но с задержкой.")]
    [SerializeField] private float _smoothTime = 0.15f;

    [Header("Debug")]
    [SerializeField] private bool _debugLog = false;

    // ---- ¬нутреннее ----
    private Vector3 _currentOffset;
    private Vector3 _offsetVelocity;
    private float _seed;
    private bool _wasMoving;

    private void Awake()
    {
        // –азный seed Ч если камер несколько, они не синхронны
        _seed = Random.Range(0f, 100f);
    }

    private void LateUpdate()
    {
        bool isMoving = _positionController != null && _positionController.IsMoving;

        if (_debugLog && isMoving != _wasMoving)
            Debug.Log($"[CameraSway] IsMoving: {isMoving}");
        _wasMoving = isMoving;

        // ¬о врем€ перелЄта камерой управл€ет OperatorPositionController
        if (isMoving)
        {
            _currentOffset = Vector3.zero;
            _offsetVelocity = Vector3.zero;
            return;
        }

        // ”бираем предыдущий offset Ч чтобы не накапливалс€
        transform.localPosition -= _currentOffset;

        float t = Time.time + _seed;

        // ƒыхание (вертикаль)
        float breathY = Mathf.Sin(t * _breathSpeedY) * _breathAmplitudeY;

        // ƒрейф (горизонталь) Ч две частоты дл€ Ђорганикиї
        float swayX = Mathf.Sin(t * _swaySpeedX) * _swayAmplitudeX
                    + Mathf.Sin(t * _swaySpeedX * 2.3f) * _swayAmplitudeX * 0.3f;

        Vector3 targetOffset = new Vector3(swayX, breathY, 0f);

        // —глаживаем, чтобы не было дрожи
        _currentOffset = Vector3.SmoothDamp(
            _currentOffset, targetOffset, ref _offsetVelocity, _smoothTime);

        // ѕримен€ем поверх базовой позиции
        transform.localPosition += _currentOffset;

        // Ќаклон головы (roll) Ч поверх peek-поворота
        float tilt = Mathf.Sin(t * _tiltSpeed) * _tiltAmplitude
                   + Mathf.Sin(t * _tiltSpeed * 1.7f) * _tiltAmplitude * 0.3f;

        // ”множаем на текущий поворот Ч CursorPeekCamera уже выставил базу
        transform.localRotation *= Quaternion.Euler(0f, 0f, tilt);
    }
}