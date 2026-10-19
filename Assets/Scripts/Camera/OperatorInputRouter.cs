using UnityEngine;

/// <summary>
/// Маршрутизатор ввода: говорит, какая позиция сейчас активна.
/// Системы должны проверять IsActive(их позиция) перед реакцией на мышь.
/// На время перелёта ввод заблокирован (CanInteract = false).
/// </summary>
public class OperatorInputRouter : MonoBehaviour
{
    [SerializeField] private OperatorPositionController _positionController;

    /// <summary>Текущая позиция. Если контроллер не назначен — Screen (безопасный дефолт).</summary>
    public OperatorPosition Current =>
        _positionController != null ? _positionController.CurrentPosition : OperatorPosition.Screen;

    /// <summary>Можно ли вообще взаимодействовать (не на перелёте)?</summary>
    public bool CanInteract =>
        _positionController != null && !_positionController.IsMoving;

    /// <summary>Активна ли конкретная позиция прямо сейчас?</summary>
    public bool IsActive(OperatorPosition position) =>
        CanInteract && Current == position;
}