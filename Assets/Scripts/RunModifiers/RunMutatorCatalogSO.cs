using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>새 게임 +에서 고를 수 있는 뮤테이터 전체 목록과 추천 프리셋.
/// 배열 순서가 곧 진입 UI의 표시 순서다.</summary>
[CreateAssetMenu(
    menuName = "TowerAndDragon/Mutators/Catalog",
    fileName = "RMC_RunMutatorCatalog")]
public sealed class RunMutatorCatalogSO : ScriptableObject
{
    /// <summary>프리셋이 참조하는 (뮤테이터 id, 단계) 쌍.
    /// ValueTuple을 인스펙터 필드로 쓸 수 없어(Unity가 직렬화하지 못한다) 별도 클래스로 둔다 -
    /// 런타임 API 시그니처에서는 (string, int) 튜플을 그대로 쓴다.</summary>
    [Serializable]
    public sealed class PresetEntry
    {
        [SerializeField]
        [Tooltip("뮤테이터의 Id. 카탈로그에 없으면 프리셋 적용이 거부된다.")]
        private string _id;

        [SerializeField]
        [Tooltip("켤 단계. 1-기반이다.")]
        private int _tier = RunMutatorSO.FIRST_TIER;

        public string Id => _id;

        public int Tier => _tier;

        /// <summary>테스트·에디터 저작 스크립트가 인스펙터 없이 값을 채울 때 쓴다.</summary>
        public void Configure(string id, int tier)
        {
            _id = id;
            _tier = tier;
        }
    }

    /// <summary>"권장 조합" 한 벌. 플레이어가 13종을 하나씩 고르지 않아도 되게 한다.</summary>
    [Serializable]
    public sealed class Preset
    {
        [SerializeField]
        [Tooltip("프리셋 이름의 스트링테이블 key.")]
        private string _nameLocKey;

        [SerializeField]
        [Tooltip("이 프리셋이 켜는 (뮤테이터 id, 단계) 목록.")]
        private PresetEntry[] _entries = Array.Empty<PresetEntry>();

        public string NameLocKey => _nameLocKey;

        public PresetEntry[] Entries => _entries ?? Array.Empty<PresetEntry>();

        /// <summary>테스트·에디터 저작 스크립트가 인스펙터 없이 값을 채울 때 쓴다.</summary>
        public void Configure(string nameLocKey, PresetEntry[] entries)
        {
            _nameLocKey = nameLocKey;
            _entries = entries;
        }
    }

    [SerializeField]
    [Tooltip("표시 순서를 가진 뮤테이터 목록.")]
    private RunMutatorSO[] _mutators = Array.Empty<RunMutatorSO>();

    [SerializeField]
    [Tooltip("추천 조합 목록. 비워 둬도 된다.")]
    private Preset[] _presets = Array.Empty<Preset>();

    public RunMutatorSO[] Mutators => _mutators ?? Array.Empty<RunMutatorSO>();

    public Preset[] Presets => _presets ?? Array.Empty<Preset>();

    public bool TryFind(string id, out RunMutatorSO mutator)
    {
        if (!string.IsNullOrEmpty(id))
        {
            RunMutatorSO[] mutators = Mutators;

            for (int i = 0; i < mutators.Length; i++)
            {
                if (mutators[i] != null && mutators[i].Id == id)
                {
                    mutator = mutators[i];
                    return true;
                }
            }
        }

        mutator = null;
        return false;
    }

    public bool Contains(string id)
    {
        return TryFind(id, out _);
    }

    /// <summary>(id, 단계) 목록을 런타임 선택 목록으로 바꾼다.
    /// <b>미지 id 또는 해당 뮤테이터의 Tiers 길이를 넘는 단계가 하나라도 있으면 false</b>이며,
    /// 이때 <paramref name="selections"/>는 신뢰할 수 없다.
    ///
    /// 부분 성공을 허용하지 않는 이유: 세이브 복원은 한번 착수하면 롤백이 불가능하다.
    /// "일부 뮤테이터만 빠진 채로 복원된 런"은 저장된 난이도 점수와 실제 난이도가 어긋난 상태다.</summary>
    public bool Resolve(
        IReadOnlyList<(string Id, int Tier)> requested,
        out List<RunMutatorSelection> selections)
    {
        selections = new List<RunMutatorSelection>();

        if (requested == null)
        {
            return true;
        }

        for (int i = 0; i < requested.Count; i++)
        {
            (string id, int tier) = requested[i];

            if (!TryFind(id, out RunMutatorSO mutator))
            {
                return false;
            }

            if (!mutator.TryGetTier(tier, out _))
            {
                return false;
            }

            selections.Add(new RunMutatorSelection(mutator, tier));
        }

        return true;
    }

    /// <summary>선택 목록에 같은 <see cref="RunMutatorSO.ExclusiveGroup"/>이 둘 이상 있는지.
    ///
    /// 판정을 카탈로그에 두는 이유: 진입 UI와 세이브 복원 검사가 각자 구현하면 규칙이 갈라진다.
    /// UI에서 막은 조합이 세이브 복원에서는 통과하는(또는 그 반대) 상태를 만들지 않는다.</summary>
    public bool HasExclusiveConflict(IReadOnlyList<RunMutatorSelection> selections)
    {
        if (selections == null)
        {
            return false;
        }

        HashSet<string> seenGroups = new HashSet<string>();

        for (int i = 0; i < selections.Count; i++)
        {
            RunMutatorSO mutator = selections[i].Mutator;

            if (mutator == null || string.IsNullOrEmpty(mutator.ExclusiveGroup))
            {
                continue;
            }

            if (!seenGroups.Add(mutator.ExclusiveGroup))
            {
                return true;
            }
        }

        return false;
    }

    // 에디터 전용 검사 - CLAUDE.md 커밋 규칙 5(리터럴 규칙 미적용)에 해당한다.
    private void OnValidate()
    {
        HashSet<string> seenIds = new HashSet<string>();
        RunMutatorSO[] mutators = Mutators;

        for (int i = 0; i < mutators.Length; i++)
        {
            RunMutatorSO mutator = mutators[i];

            if (mutator == null)
            {
                Debug.LogError($"[RunMutatorCatalogSO] Mutators[{i}]가 비어 있습니다.", this);
                continue;
            }

            if (string.IsNullOrEmpty(mutator.Id))
            {
                Debug.LogError($"[RunMutatorCatalogSO] {mutator.name}의 Id가 비어 있습니다.", mutator);
            }
            else if (!seenIds.Add(mutator.Id))
            {
                Debug.LogError(
                    $"[RunMutatorCatalogSO] Id 중복: '{mutator.Id}' - 세이브 복원이 잘못된 뮤테이터를 집습니다.",
                    mutator);
            }

            if (mutator.TierCount == 0)
            {
                Debug.LogError(
                    $"[RunMutatorCatalogSO] {mutator.name}의 Tiers가 비어 있습니다 - 켤 수 없는 뮤테이터입니다.",
                    mutator);
            }
        }
    }
}
