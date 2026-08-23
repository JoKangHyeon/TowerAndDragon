using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 상태를 건 몬스터 수와 실제로 켜진 연출 수가 어긋나는 원인을 찾는다.
///
/// 의심 지점: 속성 면역(BaseMonster.ApplyStatus의 AcceptsElement)이 부여 자체를 막았는지,
/// 아니면 연출 재조정이 안 돈 것인지. 둘은 원인이 완전히 다르다.
/// </summary>
public static class DiagnoseStatusVfxCount
{
    private const string HOLD_STATUS_ID = "probe_visual_hold";

    public static void Execute()
    {
        var report = new StringBuilder();
        report.AppendLine("[DiagnoseStatusVfxCount]");

        foreach (BaseMonster monster in Object.FindObjectsByType<BaseMonster>(
            FindObjectsSortMode.None))
        {
            var receiver = monster.GetComponent<MonsterStatusReceiver>();

            string data = monster.Data != null ? monster.Data.name : "<데이터 없음>";
            bool hasHold = receiver != null && receiver.HasStatus(HOLD_STATUS_ID);

            report.AppendLine(
                $"  {monster.name}: data={data} " +
                $"상태보유={hasHold} 사망={monster.IsDead} 활성={monster.gameObject.activeInHierarchy}");
        }

        GameObject pool = GameObject.Find("ProjectilePool (auto)");

        if (pool == null)
        {
            report.AppendLine("  풀 없음");
        }
        else
        {
            report.AppendLine($"  풀 자식 수: {pool.transform.childCount}");

            foreach (Transform child in pool.transform)
            {
                report.AppendLine(
                    $"    {child.name} active={child.gameObject.activeSelf} pos={child.position}");
            }
        }

        Debug.Log(report.ToString());
    }
}
