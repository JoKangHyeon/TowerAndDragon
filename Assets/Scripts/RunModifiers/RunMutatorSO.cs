using System;
using UnityEngine;

/// <summary>뮤테이터의 한 단계. 단계마다 점수·설명·효과가 모두 다르다.</summary>
[Serializable]
public sealed class RunMutatorTier
{
    [SerializeField]
    [Tooltip("이 단계가 난이도 점수에 기여하는 값. 설계서 §6.0의 가중치 표를 따른다.")]
    private int _difficultyScore;

    [SerializeField]
    [Tooltip("이 단계의 설명 스트링테이블 key. 단계마다 수치가 다르므로 단계마다 따로 둔다.")]
    private string _descLocKey;

    [SerializeField]
    [Tooltip("이 단계에서 적용되는 효과 목록.")]
    private RunMutatorEffectSO[] _effects = Array.Empty<RunMutatorEffectSO>();

    public int DifficultyScore => _difficultyScore;

    public string DescLocKey => _descLocKey;

    public RunMutatorEffectSO[] Effects => _effects ?? Array.Empty<RunMutatorEffectSO>();

    /// <summary>테스트·에디터 저작 스크립트가 인스펙터 없이 값을 채울 때 쓴다.</summary>
    public void Configure(int difficultyScore, string descLocKey, RunMutatorEffectSO[] effects)
    {
        _difficultyScore = difficultyScore;
        _descLocKey = descLocKey;
        _effects = effects;
    }
}

/// <summary>새 게임 +에서 켤 수 있는 뮤테이터 하나. 단계별 효과를 배열로 갖는다.
///
/// <b>단계를 별도 에셋으로 쪼개지 않은 이유</b>: 수치 뮤테이터 8종 × 3단계 = 24개 에셋이 되고
/// 한 뮤테이터의 밸런스가 3개 파일에 흩어진다. 게다가 <see cref="Tiers"/> 배열 길이가 곧 단계 수라서,
/// 설계서 §6의 "3단계를 밸런싱할 수 없다면 2단계까지여도 괜찮음"이 <b>코드 변경 없이 데이터로</b>
/// 해결된다 - 배열에 2개만 넣으면 그 뮤테이터는 2단계까지만 있는 것이 된다.
/// 길이가 1이면 켜짐/꺼짐 뮤테이터(= 규칙 계열)다.</summary>
[CreateAssetMenu(
    menuName = "TowerAndDragon/Mutators/Mutator",
    fileName = "RM_Mutator")]
public sealed class RunMutatorSO : ScriptableObject
{
    /// <summary>단계 번호의 최솟값. 1-기반이므로 1이다.</summary>
    public const int FIRST_TIER = 1;

    /// <summary>"선택하지 않음"을 나타내는 단계 번호.</summary>
    public const int UNSELECTED_TIER = 0;

    [SerializeField]
    [Tooltip("세이브 파일에 기록되는 식별자. 한번 정하면 절대 변경 금지 - "
        + "바꾸면 기존 세이브가 이 뮤테이터를 미지 id로 보고 로드를 거부한다.")]
    private string _id;

    [SerializeField]
    [Tooltip("뮤테이터 이름의 스트링테이블 key.")]
    private string _nameLocKey;

    [SerializeField]
    [Tooltip("같은 그룹의 뮤테이터끼리는 동시에 켤 수 없다. 빈 값이면 제약 없음.")]
    private string _exclusiveGroup;

    [SerializeField]
    [Tooltip("단계 목록. 배열 길이가 곧 단계 수다(1~3). 길이 1이면 켜짐/꺼짐 뮤테이터.")]
    private RunMutatorTier[] _tiers = Array.Empty<RunMutatorTier>();

    /// <summary>세이브 파일에 기록되는 식별자. <b>한번 정하면 절대 변경 금지다</b> -
    /// 바꾸면 기존 세이브의 id가 카탈로그에서 조회되지 않아 로드가 거부된다.</summary>
    public string Id => _id;

    public string NameLocKey => _nameLocKey;

    /// <summary>같은 값을 가진 뮤테이터끼리 상호배타. 빈 값이면 제약이 없다.
    /// 실제 판정은 <see cref="RunMutatorCatalogSO.HasExclusiveConflict"/>가 한다.</summary>
    public string ExclusiveGroup => _exclusiveGroup;

    public RunMutatorTier[] Tiers => _tiers ?? Array.Empty<RunMutatorTier>();

    /// <summary>단계 수. 0이면 저작이 덜 된 에셋이다.</summary>
    public int TierCount => Tiers.Length;

    /// <summary>단계를 가져온다. <paramref name="tier"/>는 <b>1-기반</b>이다.
    ///
    /// 0-인덱스를 쓰지 않는 이유: 선택 상태가 세이브에 (Id, Tier) 쌍으로 기록되므로
    /// 0-기반이면 저장된 0이 "1단계를 켰다"인지 "안 켰다"인지 구분되지 않는다.
    /// 1-기반이면 0이 곧 미선택(<see cref="UNSELECTED_TIER"/>)이다.</summary>
    public bool TryGetTier(int tier, out RunMutatorTier resolved)
    {
        RunMutatorTier[] tiers = Tiers;

        if (tier < FIRST_TIER || tier > tiers.Length)
        {
            resolved = null;
            return false;
        }

        resolved = tiers[tier - FIRST_TIER];
        return resolved != null;
    }

    /// <summary>테스트·에디터 저작 스크립트가 인스펙터 없이 값을 채울 때 쓴다.</summary>
    public void Configure(string id, string nameLocKey, string exclusiveGroup, RunMutatorTier[] tiers)
    {
        _id = id;
        _nameLocKey = nameLocKey;
        _exclusiveGroup = exclusiveGroup;
        _tiers = tiers;
    }
}
