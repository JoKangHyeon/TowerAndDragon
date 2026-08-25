using UnityEngine;

/// <summary>뮤테이터 한 단계가 갖는 효과 하나. <see cref="ResearchEffectSO"/>와 같은 관용구다 -
/// 추상 베이스가 항등값을 돌려주는 virtual 게터를 <b>전부</b> 갖고, 서브클래스는 자기 것만
/// override하고, 집계자(<see cref="RunModifierResolver"/>·<see cref="RunModifierService"/>)가 pull한다.
///
/// 효과 종류마다 별도 인터페이스를 두지 않는 이유는 집계자가 is 캐스팅으로 분기하지 않아도 되고,
/// 새 효과 종류를 추가할 때 집계자를 고칠 필요가 없기 때문이다(베이스에 게터를 하나 더 얹으면 된다).</summary>
public abstract class RunMutatorEffectSO : ScriptableObject
{
    public virtual float GetMultiplier(RunModifierChannel channel)
    {
        return 1f;
    }

    public virtual int GetCounterDelta(RunCounterChannel channel)
    {
        return 0;
    }

    public virtual RunRuleFlag GetRules()
    {
        return RunRuleFlag.None;
    }

    /// <summary>런 전역으로 적용할 적 강화 프로필. null이면 적 강화 효과가 아니다.
    /// 스냅샷에 담지 않고 EnemyEnhancementManager가 자기 목록에 병합한다 - 그래야
    /// PortalWavePreviewRenderer가 강화된 수치를 자동으로 표시한다.</summary>
    public virtual EnemyEnhancementProfileSO GetEnemyProfile()
    {
        return null;
    }

    /// <summary>지형 페널티 배율. 1보다 큰 값이 페널티 심화다(ITerrainPenaltyScaleQuery 계약).
    ///
    /// <b>이것이 스냅샷 채널이 아닌 이유</b>: 값이 (지형 × 페널티 종류) 2차원이라
    /// 1차원 채널 배열에 들어가지 않는다. 지형 5종 × 종류 4종을 채널로 펼치면 20개가 되고,
    /// 그중 실제로 쓰이는 조합은 두세 개뿐이다. 대신 서비스가 활성 효과를 직접 순회해
    /// TerrainPenaltyScaleComposite에 꽂는다.</summary>
    public virtual float GetTerrainPenaltyScale(TerrainType terrain, TerrainPenaltyKind kind)
    {
        return 1f;
    }
}
