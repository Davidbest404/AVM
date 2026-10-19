using System;
using UnityEngine;

/// <summary>
/// Гнездо для лампы. Знает, пустое ли оно и куда вставлять лампу.
/// </summary>
public class LampSocket : MonoBehaviour
{
    [Tooltip("Точка установки. Если пусто — сам объект.")]
    [SerializeField] private Transform _installPoint;

    [Tooltip("Коллайдер сокета (по нему идёт raycast при вставке). Если пусто — ищется на объекте.")]
    [SerializeField] private Collider _socketCollider;

    public Lamp CurrentLamp { get; private set; }
    public bool IsEmpty => CurrentLamp == null;

    public Transform InstallPoint => _installPoint != null ? _installPoint : transform;
    public Collider SocketCollider => _socketCollider != null ? _socketCollider : GetComponent<Collider>();

    public event Action<LampSocket> OnLampChanged;

    public void SetLamp(Lamp lamp)
    {
        CurrentLamp = lamp;
        OnLampChanged?.Invoke(this);
    }

    public void ClearLamp()
    {
        CurrentLamp = null;
        OnLampChanged?.Invoke(this);
    }
}