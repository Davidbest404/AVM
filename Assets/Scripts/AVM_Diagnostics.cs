using System.Collections;
using System.Reflection;
using System.Text;
using UnityEngine;

/// <summary>
/// Диагностический отчёт: все системы в GameManager и все события в EventGenerator.
/// Ждёт один кадр, чтобы GameManager.Start успел зарегистрировать системы.
/// </summary>
public class AVM_Diagnostics : MonoBehaviour
{
    private IEnumerator Start()
    {
        // Ждём кадр — GameManager.Start должен отработать раньше
        yield return null;
        yield return null;

        DumpSystems();
        DumpEvents();
    }

    private void DumpSystems()
    {
        var gm = GameManager.Instance;
        if (gm == null)
        {
            Debug.LogError("[AVM_Diagnostics] GameManager.Instance = null");
            return;
        }

        var field = typeof(GameManager).GetField("_systems",
            BindingFlags.NonPublic | BindingFlags.Instance);
        var systems = field?.GetValue(gm) as IList;

        var sb = new StringBuilder();
        sb.AppendLine("=== СИСТЕМЫ В GAMEMANAGER (после Start) ===");

        if (systems == null || systems.Count == 0)
        {
            sb.AppendLine("  <пусто> — ни одна система не зарегистрирована!");
        }
        else
        {
            foreach (var s in systems)
            {
                if (s is IMachineSystem ms)
                {
                    string failable = s is IFailableSystem ? "IFailable=ДА" : "IFailable=НЕТ";
                    sb.AppendLine($"  '{ms.SystemName}'  ({s.GetType().Name})  " +
                                  $"{failable}  IsBroken={ms.IsBroken}");
                }
                else
                {
                    sb.AppendLine($"  ??? {s.GetType().Name} (не IMachineSystem)");
                }
            }
        }

        Debug.Log(sb.ToString());
    }

    private void DumpEvents()
    {
        var gen = FindFirstObjectByType<EventGenerator>();
        if (gen == null)
        {
            Debug.LogError("[AVM_Diagnostics] EventGenerator не найден на сцене");
            return;
        }

        var field = typeof(EventGenerator).GetField("_eventPool",
            BindingFlags.NonPublic | BindingFlags.Instance);
        var pool = field?.GetValue(gen) as IList;

        var sb = new StringBuilder();
        sb.AppendLine("=== ПУЛ СОБЫТИЙ ===");

        if (pool == null || pool.Count == 0)
        {
            sb.AppendLine("  <пусто>");
        }
        else
        {
            foreach (var e in pool)
            {
                var ev = e as FailureEvent;
                if (ev == null) { sb.AppendLine("  <null>"); continue; }

                string target = "";
                if (ev is SystemFailureEvent sys)
                {
                    var gameSys = GameManager.Instance?.GetSystem<IFailableSystem>(sys.targetSystemName);
                    target = gameSys != null
                        ? $"  target='{sys.targetSystemName}' → НАЙДЕНА (IsBroken={gameSys.IsBroken})"
                        : $"  target='{sys.targetSystemName}' → НЕ НАЙДЕНА!";
                }

                bool can = ev.CanTrigger();
                sb.AppendLine($"  '{ev.displayName}'  w={ev.weight}  " +
                              $"prog[{ev.minProgress}..{ev.maxProgress}]  " +
                              $"CanTrigger={can}{target}");
            }
        }

        Debug.Log(sb.ToString());
    }
}