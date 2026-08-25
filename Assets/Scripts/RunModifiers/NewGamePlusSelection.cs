using System.Collections.Generic;

/// <summary>진입 UI가 고르고 있는 "어떤 뮤테이터를 몇 단계로 켤 것인가"의 상태.
///
/// MonoBehaviour가 아닌 순수 C#인 이유: 단계 범위 검사·점수 합계·상호배타 판정·프리셋 적용은
/// 화면과 아무 상관이 없는 규칙이고, 카탈로그에 아직 규칙 계열 뮤테이터도 프리셋도 없어
/// (T6이 저작한다) 실데이터로는 확인할 수 없다. 여기로 빼 두면 EditMode 테스트가
/// <see cref="RunMutatorSO.Configure"/>로 임의의 데이터를 만들어 검증할 수 있다.
///
/// 인덱스는 <see cref="RunMutatorCatalogSO.Mutators"/>의 순서와 정렬돼 있다 - 목록 UI가
/// 슬롯 번호를 그대로 인덱스로 쓸 수 있게. 단계는 <b>1-기반</b>이며
/// <see cref="RunMutatorSO.UNSELECTED_TIER"/>(0)이 미선택이다.</summary>
public sealed class NewGamePlusSelection
{
    private readonly RunMutatorCatalogSO _catalog;

    // 비어 있는 항목을 걸러낸 목록. 걸러 두면 아래의 어느 경로도 null 뮤테이터를 다루지 않는다
    // (카탈로그의 OnValidate가 비어 있는 항목을 에러로 보고하므로, 여기서는 조용히 뺀다).
    private readonly RunMutatorSO[] _mutators;

    private readonly int[] _tiers;

    private readonly Dictionary<string, int> _indexById = new();

    // 점수 합계·상호배타 판정에 넘길 목록. 매 렌더마다 새로 할당하지 않도록 재사용한다.
    private readonly List<RunMutatorSelection> _selectionBuffer = new();

    public NewGamePlusSelection(RunMutatorCatalogSO catalog)
    {
        _catalog = catalog;

        List<RunMutatorSO> valid = new();

        if (catalog != null)
        {
            foreach (RunMutatorSO mutator in catalog.Mutators)
            {
                if (mutator != null && mutator.TierCount > 0)
                {
                    valid.Add(mutator);
                }
            }
        }

        _mutators = valid.ToArray();
        _tiers = new int[_mutators.Length];

        for (int i = 0; i < _mutators.Length; i++)
        {
            // 중복 id는 카탈로그 OnValidate가 에러로 보고한다. 여기서는 먼저 온 것을 남긴다.
            if (!_indexById.ContainsKey(_mutators[i].Id))
            {
                _indexById.Add(_mutators[i].Id, i);
            }
        }
    }

    /// <summary>고를 수 있는 뮤테이터 수. 곧 목록 슬롯 수다.</summary>
    public int Count => _mutators.Length;

    /// <summary>선택된 단계 점수의 합. 합산을 여기서 다시 구현하지 않고
    /// <see cref="RunModifierResolver.ResolveDifficultyScore"/>를 쓴다 - 실제 런의 점수와
    /// 화면의 점수가 갈라지지 않게 한다.</summary>
    public int DifficultyScore => RunModifierResolver.ResolveDifficultyScore(BuildSelections());

    public RunMutatorSO MutatorAt(int index) => _mutators[index];

    public int TierAt(int index) => _tiers[index];

    public bool IsSelected(int index) => _tiers[index] != RunMutatorSO.UNSELECTED_TIER;

    /// <summary>단계를 바꾼다. <paramref name="tier"/>가 0이면 선택 해제다.
    ///
    /// 범위 밖 단계이거나 상호배타에 걸리면 <b>아무것도 바꾸지 않고</b> false를 돌려준다 -
    /// 반쯤 적용된 상태를 만들지 않는다. 상호배타 판정은
    /// <see cref="RunMutatorCatalogSO.HasExclusiveConflict"/>에 맡긴다(UI에서 재구현하지 않는다).</summary>
    public bool TrySetTier(int index, int tier)
    {
        if (index < 0 || index >= _mutators.Length)
        {
            return false;
        }

        if (tier != RunMutatorSO.UNSELECTED_TIER && !_mutators[index].TryGetTier(tier, out _))
        {
            return false;
        }

        int previous = _tiers[index];

        if (previous == tier)
        {
            return true;
        }

        _tiers[index] = tier;

        if (_catalog != null && _catalog.HasExclusiveConflict(BuildSelections()))
        {
            _tiers[index] = previous;
            return false;
        }

        return true;
    }

    /// <summary>프리셋 하나를 그대로 적용한다. 미지 id·범위 밖 단계·상호배타가 하나라도 있으면
    /// <b>아무것도 바꾸지 않고</b> false다 - 부분 적용된 프리셋은 화면의 점수와 플레이어의
    /// 의도가 어긋난 상태다(<see cref="RunMutatorCatalogSO.Resolve"/>가 부분 성공을 허용하지 않는 것과 같은 이유).</summary>
    public bool TryApplyPreset(RunMutatorCatalogSO.Preset preset)
    {
        if (preset == null || _catalog == null)
        {
            return false;
        }

        List<(string Id, int Tier)> requested = new();

        foreach (RunMutatorCatalogSO.PresetEntry entry in preset.Entries)
        {
            if (entry == null)
            {
                return false;
            }

            requested.Add((entry.Id, entry.Tier));
        }

        if (!_catalog.Resolve(requested, out List<RunMutatorSelection> resolved))
        {
            return false;
        }

        if (_catalog.HasExclusiveConflict(resolved))
        {
            return false;
        }

        Clear();

        foreach (RunMutatorSelection selection in resolved)
        {
            // Resolve가 통과했어도 이 인스턴스가 걸러낸 항목(단계 0개짜리)일 수 있다.
            if (_indexById.TryGetValue(selection.Mutator.Id, out int index))
            {
                _tiers[index] = selection.Tier;
            }
        }

        return true;
    }

    public void Clear()
    {
        for (int i = 0; i < _tiers.Length; i++)
        {
            _tiers[i] = RunMutatorSO.UNSELECTED_TIER;
        }
    }

    /// <summary>이 선택이 프리셋과 정확히 같은 조합인가. 어떤 프리셋 버튼이 눌린 상태인지 표시하는 데 쓴다.</summary>
    public bool MatchesPreset(RunMutatorCatalogSO.Preset preset)
    {
        if (preset == null)
        {
            return false;
        }

        int matched = 0;

        foreach (RunMutatorCatalogSO.PresetEntry entry in preset.Entries)
        {
            if (entry == null
                || !_indexById.TryGetValue(entry.Id, out int index)
                || _tiers[index] != entry.Tier)
            {
                return false;
            }

            matched++;
        }

        return matched == SelectedCount;
    }

    /// <summary>켜진 뮤테이터 수.</summary>
    public int SelectedCount
    {
        get
        {
            int count = 0;

            for (int i = 0; i < _tiers.Length; i++)
            {
                if (_tiers[i] != RunMutatorSO.UNSELECTED_TIER)
                {
                    count++;
                }
            }

            return count;
        }
    }

    /// <summary><see cref="NewGamePlusRequest.Request"/>에 그대로 넘길 (id, 단계) 목록.
    /// 미선택 항목은 빠진다.</summary>
    public List<(string Id, int Tier)> BuildRequest()
    {
        List<(string Id, int Tier)> request = new();

        for (int i = 0; i < _mutators.Length; i++)
        {
            if (_tiers[i] != RunMutatorSO.UNSELECTED_TIER)
            {
                request.Add((_mutators[i].Id, _tiers[i]));
            }
        }

        return request;
    }

    /// <summary>현재 단계의 설명 key. 미선택이면 첫 단계의 설명을 보여준다 - 켜면 무슨 일이
    /// 일어나는지 미리 읽을 수 있어야 고를 수 있다.</summary>
    public string DescLocKeyAt(int index)
    {
        return TryGetShownTier(index, out RunMutatorTier tier) ? tier.DescLocKey : string.Empty;
    }

    /// <summary>현재 단계의 점수. 미선택이면 첫 단계의 점수(= 켰을 때 붙는 점수)를 보여준다.</summary>
    public int ShownScoreAt(int index)
    {
        return TryGetShownTier(index, out RunMutatorTier tier) ? tier.DifficultyScore : 0;
    }

    // 미선택 상태에서도 무엇을 켜는지 보여줘야 하므로, 표시용 단계는 "선택된 단계, 없으면 1단계"다.
    private bool TryGetShownTier(int index, out RunMutatorTier tier)
    {
        if (index < 0 || index >= _mutators.Length)
        {
            tier = null;
            return false;
        }

        int shown = IsSelected(index) ? _tiers[index] : RunMutatorSO.FIRST_TIER;
        return _mutators[index].TryGetTier(shown, out tier);
    }

    private List<RunMutatorSelection> BuildSelections()
    {
        _selectionBuffer.Clear();

        for (int i = 0; i < _mutators.Length; i++)
        {
            if (_tiers[i] != RunMutatorSO.UNSELECTED_TIER)
            {
                _selectionBuffer.Add(new RunMutatorSelection(_mutators[i], _tiers[i]));
            }
        }

        return _selectionBuffer;
    }
}
