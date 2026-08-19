using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 이번 밤 "모든 적"에게 걸린 강화를 툴팁 줄로 만든다. 지금은 점령 기반 강화가 유일한 출처지만,
/// 하드모드 전역 버프처럼 새 출처가 생기면 여기 한 곳만 늘리면 낮의 출현 예고 카드와
/// 밤의 개체 호버 툴팁에 동시에 나온다 - 표시 코드를 두 번 고치지 않는 것이 이 클래스의 목적이다.
///
/// 개체 상태이상(MonsterStatusReceiver)과 같은 MonsterStatusLine을 만들되 Scope만 Global로 둔다.
/// </summary>
public static class EnemyEnhancementStatusLines
{
    // 배율을 백분율로 적는다. 1.3배는 "+30%"가 플레이어에게 곧바로 읽힌다.
    private const float PERCENT_SCALE = 100f;
    private const string PERCENT_FORMAT = "+0.#;-0.#";

    // 부동소수 오차로 "+0%" 줄이 뜨는 것을 막는 하한. 이보다 작은 차이는 강화가 없는 것으로 본다.
    private const float MEANINGFUL_PERCENT = 0.5f;

    /// <summary>강화 줄을 buffer에 덧붙인다(비우지 않는다 - 개체 줄과 한 목록에 모은다).</summary>
    public static void Collect(in EnemyEnhancementSnapshot snapshot, List<MonsterStatusLine> buffer)
    {
        if (buffer == null)
        {
            return;
        }

        AppendModifier(buffer, StatusLocKeys.ENHANCEMENT_MAX_HEALTH, snapshot.MaxHealth);
        AppendModifier(buffer, StatusLocKeys.ENHANCEMENT_SHIELD, snapshot.ShieldAmount);
        AppendModifier(buffer, StatusLocKeys.ENHANCEMENT_ATTACK_POWER, snapshot.AttackPower);
        AppendModifier(buffer, StatusLocKeys.ENHANCEMENT_MOVE_SPEED, snapshot.MoveSpeed);
    }

    // 스폰 수·스폰 간격은 적지 않는다 - 적 한 마리를 설명하는 툴팁에서 "이 적이 몇 마리 나오는가"는
    // 그 적의 상태가 아니라 웨이브 구성이고, 예고 카드는 이미 마릿수를 따로 보여준다.
    private static void AppendModifier(
        List<MonsterStatusLine> buffer,
        string labelLocKey,
        in ResolvedEnemyStatModifier modifier)
    {
        // 가산 보정은 스탯마다 단위가 달라(체력 +50 vs 이동속도 +0.5) 한 줄로 묶어 적을 수 없다.
        // 대신 최종 수치 자체가 강화를 이미 반영하므로, 여기서는 배율만 "얼마나 세졌는지"로 적는다.
        float percent = (modifier.Multiplier - 1f) * PERCENT_SCALE;

        if (Mathf.Abs(percent) < MEANINGFUL_PERCENT)
        {
            return;
        }

        buffer.Add(new MonsterStatusLine(
            MonsterStatusScope.Global,
            StringTable.GetString(labelLocKey),
            string.Format(
                StringTable.GetString(StatusLocKeys.ENHANCEMENT_PERCENT),
                percent.ToString(PERCENT_FORMAT))));
    }
}
