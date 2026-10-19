using UnityEngine;

/// <summary>
/// Ящик с запасными лампами. По клику спавнит новую лампу в точке выдачи.
/// </summary>
public class LampBox : MonoBehaviour
{
    [Tooltip("Префаб лампы (Parent = Bulb + Screw + коллайдер).")]
    [SerializeField] private Lamp _lampPrefab;

    [Tooltip("Точка, откуда появляется лампа. Если пусто — позиция ящика.")]
    [SerializeField] private Transform _spawnPoint;

    [Tooltip("Коллайдер ящика (по нему идёт raycast). Если пусто — ищется на объекте.")]
    [SerializeField] private Collider _boxCollider;

    public Collider BoxCollider => _boxCollider != null ? _boxCollider : GetComponent<Collider>();

    public Lamp SpawnLamp()
    {
        if (_lampPrefab == null)
        {
            Debug.LogError("[LampBox] Не назначен _lampPrefab");
            return null;
        }

        Vector3 pos = _spawnPoint != null ? _spawnPoint.position : transform.position;
        Lamp lamp = Instantiate(_lampPrefab, pos, Quaternion.identity);
        lamp.SetState(LampState.Working);
        return lamp;
    }
}