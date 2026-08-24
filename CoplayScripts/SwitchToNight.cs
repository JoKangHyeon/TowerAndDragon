using UnityEngine;

public static class SwitchToNight
{
    public static void Execute()
    {
        if (!Application.isPlaying)
        {
            Debug.LogError("[N] 플레이 모드가 아닙니다.");
            return;
        }

        var cycle = Object.FindFirstObjectByType<CycleManager>();
        if (cycle == null)
        {
            Debug.LogError("[N] CycleManager 없음");
            return;
        }

        cycle.StartNight();
        Debug.Log($"[N] 주기 = {cycle.CurrentCycle}");
    }

    public static void ReportButtons()
    {
        var window = Object.FindFirstObjectByType<UI_PopulationAllocationWindow>();
        if (window == null)
        {
            Debug.LogError("[N] 창 없음");
            return;
        }

        foreach (UnityEngine.UI.Button button in window.GetComponentsInChildren<UnityEngine.UI.Button>(true))
        {
            Debug.Log($"[N] BUTTON {button.name} interactable={button.interactable}");
        }

        foreach (TMPro.TMP_Text text in window.GetComponentsInChildren<TMPro.TMP_Text>(true))
        {
            if (text.name.Contains("NightLocked") || text.transform.parent.name.Contains("NightLocked"))
            {
                Debug.Log($"[N] NIGHTLOCK active={text.gameObject.activeSelf} '{text.text}'");
            }
        }
    }
}
