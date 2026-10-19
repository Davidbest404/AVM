/// <summary>
/// Система, которая может временно блокировать весь ввод (например, режим замены).
/// GameManager опрашивает все системы на этот интерфейс.
/// </summary>
public interface IInputBlockingSystem : IMachineSystem
{
    bool IsBlockingInput();
}