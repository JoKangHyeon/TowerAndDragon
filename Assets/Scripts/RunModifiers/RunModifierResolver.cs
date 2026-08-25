using System;
using System.Collections.Generic;

/// <summary>선택된 뮤테이터 목록을 <see cref="RunModifierSnapshot"/> 하나로 합성한다.
/// 곱연산 채널은 곱, 가산 채널은 합, 규칙 플래그는 OR로 누적한다.
///
/// <see cref="EnemyEnhancementResolver"/>와 같은 모양의 순수 static 클래스이며
/// <b>MonoBehaviour를 모른다.</b> 설계서 §11이 요구한 "뮤테이터 목록 → 스냅샷 합성 결과"
/// 자동 테스트를 씬 없이 EditMode에서 돌리기 위해서다 - 계산이 Awake 안에 있으면
/// 테스트가 씬을 띄우고 컴포넌트를 배선해야 한다.
///
/// <b>지형 페널티와 적 프로필은 스냅샷에 넣지 않는다.</b> 전자는 (지형 × 종류) 2차원이라
/// 1차원 채널 배열에 안 들어가고, 후자는 EnemyEnhancementManager가 자기 목록에 병합해야
/// 웨이브 미리보기가 자동으로 따라온다. 둘 다 <see cref="RunModifierService"/>가 따로 pull한다.</summary>
public static class RunModifierResolver
{
    public static RunModifierSnapshot Resolve(IReadOnlyList<RunMutatorSelection> selections)
    {
        if (selections == null || selections.Count == 0)
        {
            return RunModifierSnapshot.Neutral;
        }

        float[] multipliers = RunModifierSnapshot.CreateNeutralMultipliers();
        int[] counters = RunModifierSnapshot.CreateNeutralCounters();
        RunRuleFlag rules = RunRuleFlag.None;

        RunModifierChannel[] multiplierChannels =
            (RunModifierChannel[])Enum.GetValues(typeof(RunModifierChannel));
        RunCounterChannel[] counterChannels =
            (RunCounterChannel[])Enum.GetValues(typeof(RunCounterChannel));

        for (int i = 0; i < selections.Count; i++)
        {
            RunMutatorSelection selection = selections[i];

            // 선택 목록에 빈 원소나 범위 밖 단계가 섞여 있어도 나머지는 정상 합성한다 -
            // 카탈로그 검사를 이미 통과한 목록이 오는 것이 정상이지만, 여기서 예외를 던지면
            // 한 항목의 저작 실수가 런 전체를 못 시작하게 만든다.
            if (selection.Mutator == null
                || !selection.Mutator.TryGetTier(selection.Tier, out RunMutatorTier tier))
            {
                continue;
            }

            foreach (RunMutatorEffectSO effect in tier.Effects)
            {
                if (effect == null)
                {
                    continue;
                }

                for (int channelIndex = 0; channelIndex < multiplierChannels.Length; channelIndex++)
                {
                    multipliers[channelIndex] *= effect.GetMultiplier(multiplierChannels[channelIndex]);
                }

                for (int channelIndex = 0; channelIndex < counterChannels.Length; channelIndex++)
                {
                    counters[channelIndex] += effect.GetCounterDelta(counterChannels[channelIndex]);
                }

                rules |= effect.GetRules();
            }
        }

        return new RunModifierSnapshot(multipliers, counters, rules);
    }

    /// <summary>선택된 단계들의 난이도 점수 합. 스냅샷과 달리 표시·기록용이므로 따로 둔다.</summary>
    public static int ResolveDifficultyScore(IReadOnlyList<RunMutatorSelection> selections)
    {
        if (selections == null)
        {
            return 0;
        }

        int score = 0;

        for (int i = 0; i < selections.Count; i++)
        {
            RunMutatorSelection selection = selections[i];

            if (selection.Mutator == null
                || !selection.Mutator.TryGetTier(selection.Tier, out RunMutatorTier tier))
            {
                continue;
            }

            score += tier.DifficultyScore;
        }

        return score;
    }
}
