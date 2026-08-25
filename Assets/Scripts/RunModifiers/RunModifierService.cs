using System.Collections.Generic;
using UnityEngine;

/// <summary>런 시작 시 선택된 뮤테이터를 합성해 보관하고, 기존 합성기에 런 단위 출처로 꽂히는 서비스.
///
/// <b>왜 소비 지점을 고치지 않고 Composite에 꽂는가</b> - 청크 생산과 지형 페널티에는 이미
/// 합성기(<see cref="ChunkYieldMultiplierComposite"/>·<see cref="TerrainPenaltyScaleComposite"/>)가
/// 있고, 예측(ResourceForecast)·배치 툴팁(UI_PlacementYieldTooltipDriver)·실지급이 모두
/// 그 합성기를 통해 같은 값을 읽는다. 여기에 출처 하나를 등록하면 <b>세 경로가 자동으로 일치한다.</b>
/// 반대로 소비 지점을 각각 고치면 하나를 빠뜨렸을 때 화면에 보이는 값과 실제 지급값이 어긋나고,
/// 그 어긋남은 컴파일 에러도 로그도 남기지 않는다.
///
/// <see cref="ITerrainPenaltyScaleQuery"/>는 주석에 이미 "1보다 큰 값 = 페널티 심화"를 계약으로
/// 정의해 두었다. 새끼용이 완화(0~1)로 쓰는 바로 그 슬롯에 반대 방향 출처를 꽂는 것뿐이다.
///
/// <b>Tutorial 씬에는 이 컴포넌트를 두지 않는다.</b> 튜토리얼은 새 게임 + 대상이 아니며,
/// 소비 지점의 참조가 모두 <c>[WiringOptional]</c>이라 미배선이 곧 "뮤테이터 없음"으로 안전하게
/// 퇴화한다(<see cref="RunModifiers.SnapshotOf"/>가 null을 Neutral로 바꾼다).
/// Tutorial 씬에 <see cref="TerrainPenaltyScaleComposite"/> 자체가 없는 것도 같은 이유이며 정상이다.</summary>
public sealed class RunModifierService : MonoBehaviour,
    IChunkYieldMultiplierQuery,
    ITerrainPenaltyScaleQuery
{
    [SerializeField]
    [Tooltip("뮤테이터 id를 실제 에셋으로 조회하는 카탈로그.")]
    private RunMutatorCatalogSO _catalog;

    [SerializeField]
    [Tooltip("청크 생산 배율 합성기. 비워 두면 생산 계열 뮤테이터가 적용되지 않는다.")]
    [WiringOptional]
    private ChunkYieldMultiplierComposite _yieldComposite;

    [SerializeField]
    [Tooltip("지형 페널티 합성기. 비워 두면 지형 계열 뮤테이터가 적용되지 않는다.")]
    [WiringOptional]
    private TerrainPenaltyScaleComposite _terrainPenaltyComposite;

    private readonly List<RunMutatorSelection> _activeSelections = new();

    private RunModifierSnapshot _activeSnapshot = RunModifierSnapshot.Neutral;

    /// <summary>합성이 끝난 런 단위 수정치. 뮤테이터가 없으면 <see cref="RunModifierSnapshot.Neutral"/>이다.</summary>
    public RunModifierSnapshot ActiveSnapshot => _activeSnapshot;

    public IReadOnlyList<RunMutatorSelection> ActiveSelections => _activeSelections;

    /// <summary>선택된 단계들의 난이도 점수 합. 해금 기록(MetaProgress)과 표시에 쓴다.</summary>
    public int DifficultyScore { get; private set; }

    /// <summary>뮤테이터가 하나라도 켜져 있는가. 표준 모드와 구분해 표시할 때 쓴다.</summary>
    public bool IsNewGamePlus => _activeSelections.Count > 0;

    /// <summary>세이브에서 복원된 (id, 단계) 목록을 적용한다. 세이브 복원 경로(T3)가 호출한다.
    /// 새 런 시작은 <see cref="NewGamePlusRequest"/>를 통하므로 이 메서드를 쓰지 않는다.</summary>
    public void ApplyRestoredMutators(IReadOnlyList<(string Id, int Tier)> selections)
    {
        Apply(selections);
    }

    /// <summary>이 목록이 카탈로그로 전부 해석되는지만 본다. <b>아무 상태도 바꾸지 않는다.</b>
    ///
    /// 세이브 복원 착수 전 거부 판정에 쓴다(SaveService). <see cref="ApplyRestoredMutators"/>는
    /// 해석에 실패하면 로그만 남기고 표준 모드로 퇴화하는데, 복원은 한 번 시작하면 롤백이 불가능하므로
    /// 그때는 이미 늦다 - 제약이 조용히 빠진 채 런이 이어지면 그것이 곧 치트다.</summary>
    public bool CanApplyRestoredMutators(IReadOnlyList<(string Id, int Tier)> selections)
    {
        if (selections == null || selections.Count == 0)
        {
            return true;
        }

        return WiringGuard.Require(_catalog, nameof(_catalog), this) &&
            _catalog.Resolve(selections, out _);
    }

    // 청크 좌표와 자원 종류를 보지 않는다 - 생산 계열 뮤테이터는 특정 청크나 특정 자원이 아니라
    // 런 전체의 생산량에 걸리는 전역 배율이기 때문이다. 인터페이스 계약상 인자는 받아 둔다.
    public float GetYieldMultiplier(Vector2Int chunkCoord, ResourceType resourceType)
    {
        return _activeSnapshot.GetMultiplier(RunModifierChannel.ChunkYield);
    }

    // worldPosition을 보지 않는다 - 새끼용 완화는 타원 반경 안에서만 유효하지만, 뮤테이터는
    // 런 전역이라 위치와 무관하게 같은 값을 낸다. 인터페이스 계약상 인자는 받아 둔다.
    public float GetPenaltyScale(Vector3 worldPosition, TerrainType terrain, TerrainPenaltyKind kind)
    {
        float scale = 1f;

        for (int i = 0; i < _activeSelections.Count; i++)
        {
            RunMutatorSelection selection = _activeSelections[i];

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

                scale *= effect.GetTerrainPenaltyScale(terrain, kind);
            }
        }

        return scale;
    }

    /// <summary>활성 뮤테이터가 지정한 런 전역 적 강화 프로필. EnemyEnhancementManager(T5)가 병합한다.</summary>
    public void CollectEnemyProfiles(List<EnemyEnhancementProfileSO> destination)
    {
        if (destination == null)
        {
            return;
        }

        for (int i = 0; i < _activeSelections.Count; i++)
        {
            RunMutatorSelection selection = _activeSelections[i];

            if (selection.Mutator == null
                || !selection.Mutator.TryGetTier(selection.Tier, out RunMutatorTier tier))
            {
                continue;
            }

            foreach (RunMutatorEffectSO effect in tier.Effects)
            {
                EnemyEnhancementProfileSO profile = effect != null ? effect.GetEnemyProfile() : null;

                if (profile != null)
                {
                    destination.Add(profile);
                }
            }
        }
    }

    // 스냅샷을 계산만 한다 - 아무 이벤트도 발화하지 않는다. 런 시작 프로필은 아직 아무것도
    // 그려지지 않은 시점이라 통지할 대상이 없고, Awake 발화는 CLAUDE.md 이벤트 규칙 위반이다.
    private void Awake()
    {
        if (!NewGamePlusRequest.TryConsume(out (string Id, int Tier)[] requested))
        {
            return;
        }

        Apply(requested);
    }

    // ChunkYieldCoordinator·BabyDragonBuffSystem과 같은 관용구 - 등록은 OnEnable, 해제는 OnDisable.
    private void OnEnable()
    {
        if (WiringGuard.Optional(_yieldComposite, nameof(_yieldComposite), this))
        {
            _yieldComposite.Register(this);
        }

        if (WiringGuard.Optional(_terrainPenaltyComposite, nameof(_terrainPenaltyComposite), this))
        {
            _terrainPenaltyComposite.Register(this);
        }
    }

    private void OnDisable()
    {
        if (_yieldComposite != null)
        {
            _yieldComposite.Unregister(this);
        }

        if (_terrainPenaltyComposite != null)
        {
            _terrainPenaltyComposite.Unregister(this);
        }
    }

    private void Apply(IReadOnlyList<(string Id, int Tier)> requested)
    {
        _activeSelections.Clear();
        _activeSnapshot = RunModifierSnapshot.Neutral;
        DifficultyScore = 0;

        if (requested == null || requested.Count == 0)
        {
            return;
        }

        if (!WiringGuard.Require(_catalog, nameof(_catalog), this))
        {
            return;
        }

        if (!_catalog.Resolve(requested, out List<RunMutatorSelection> selections))
        {
            Debug.LogError(
                "[RunModifierService] 카탈로그에 없는 뮤테이터 id 또는 범위 밖 단계가 있습니다 - 뮤테이터를 적용하지 않고 표준 모드로 시작합니다.",
                this);
            return;
        }

        _activeSelections.AddRange(selections);
        _activeSnapshot = RunModifierResolver.Resolve(_activeSelections);
        DifficultyScore = RunModifierResolver.ResolveDifficultyScore(_activeSelections);
    }
}
