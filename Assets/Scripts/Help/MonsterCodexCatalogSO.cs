using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 적 도감 항목 전체 목록. HelpCatalogSO와 같은 역할이지만 항목을 하나씩 SO로 만들지 않는다 -
/// 적 이름·설명·스탯은 이미 MonsterData가 들고 있으므로, 여기서는 "어떤 몬스터 프리팹을
/// 도감에 실을지"와 "어느 갈래에 넣을지"만 정한다.
///
/// 해금 판정(MonsterCodexDiscoveryController)과 목록 표시(UI_HelpWindow)가 같은 목록을 봐야
/// 하므로 한곳에 모은다(HelpCatalogSO 클래스 주석과 같은 이유).
/// </summary>
[CreateAssetMenu(menuName = "TowerAndDragon/Help/Monster Codex Catalog", fileName = "MonsterCodexCatalog")]
public sealed class MonsterCodexCatalogSO : ScriptableObject
{
    [Tooltip("보스가 아닌 몬스터 프리팹. 갈래는 MonsterData.MovementType으로 자동 분류한다(지상/공중).")]
    [SerializeField] private List<BaseMonster> _monsterPrefabs = new();

    [Tooltip("보스 몬스터 프리팹. 위 목록과 갈래만 다를 뿐 취급은 같다 - " +
             "이 프로젝트는 보스를 데이터 타입이 아니라 웨이브 번호로 판정하므로, " +
             "\"보스인가\"는 이 카탈로그만 아는 사실이다.")]
    [SerializeField] private List<BaseMonster> _bossPrefabs = new();

    /// <summary>적 도감 목록의 한 줄. Data와 Icon 조회에 필요한 만큼만 들고 있다.</summary>
    public readonly struct Row
    {
        public readonly MonsterData Data;
        public readonly Sprite Icon;
        public readonly MonsterCodexCategory Category;

        public Row(MonsterData data, Sprite icon, MonsterCodexCategory category)
        {
            Data = data;
            Icon = icon;
            Category = category;
        }
    }

    // 프리팹 목록에서 파생되는 값이라 직렬화 필드가 바뀌지 않는 한 그대로다. OnValidate에서만 비운다 -
    // 에디터에서 목록을 고친 직후에도 다시 빌드되게 하기 위함(PortalWavePreviewRenderer의
    // _iconByMonsterPrefab 캐시와 같은 방식이지만, 여기는 항목 자체가 적어 프리팹 단위가 아니라
    // 카탈로그 전체를 한 번에 다시 만든다).
    private List<Row> _rows;

    public IReadOnlyList<Row> Rows
    {
        get
        {
            EnsureBuilt();
            return _rows;
        }
    }

    public int TotalCount => Rows.Count;

    public bool TryGet(MonsterData data, out Row row)
    {
        if (data != null)
        {
            foreach (Row candidate in Rows)
            {
                if (candidate.Data == data)
                {
                    row = candidate;
                    return true;
                }
            }
        }

        row = default;
        return false;
    }

    /// <summary>도감 목록에 보이는 항목인가(HelpCatalogSO.IsVisible과 같은 규칙 - 해금 여부 하나).</summary>
    public static bool IsVisible(in Row row) =>
        row.Data != null && HelpProfile.IsUnlocked(row.Data.NameLocKey);

    /// <summary>목록에 보이는데 아직 펼쳐 보지 않은 항목이 하나라도 있는가(탭·HUD 붉은 점 판정).</summary>
    public bool HasUnviewedVisibleEntry()
    {
        foreach (Row row in Rows)
        {
            if (IsVisible(row) && !HelpProfile.IsViewed(row.Data.NameLocKey))
            {
                return true;
            }
        }

        return false;
    }

    private void EnsureBuilt()
    {
        if (_rows != null)
        {
            return;
        }

        _rows = new List<Row>();

        AppendRows(_monsterPrefabs, isBoss: false);
        AppendRows(_bossPrefabs, isBoss: true);
    }

    private void AppendRows(List<BaseMonster> prefabs, bool isBoss)
    {
        if (prefabs == null)
        {
            return;
        }

        foreach (BaseMonster prefab in prefabs)
        {
            if (prefab == null || prefab.Data == null)
            {
                continue;
            }

            MonsterCodexCategory category = isBoss
                ? MonsterCodexCategory.Boss
                : ResolveNonBossCategory(prefab.Data);

            _rows.Add(new Row(prefab.Data, ResolveIcon(prefab), category));
        }
    }

    private static MonsterCodexCategory ResolveNonBossCategory(MonsterData data) =>
        data.MovementType == MonsterMovementType.Air ? MonsterCodexCategory.Air : MonsterCodexCategory.Ground;

    // MonsterData에는 아이콘 필드가 없어, 인게임 프리팹의 스프라이트를 그대로 도감 아이콘으로 쓴다
    // (PortalWavePreviewRenderer.ResolveMonsterIcon과 같은 이유·같은 방식).
    private static Sprite ResolveIcon(BaseMonster prefab)
    {
        SpriteRenderer spriteRenderer = prefab.GetComponentInChildren<SpriteRenderer>(true);
        return spriteRenderer != null ? spriteRenderer.sprite : null;
    }

    // 목록을 고치고 나면 곧바로 반영되도록 캐시를 비운다. 중복 NameLocKey는 항목 하나만 봐서는
    // 알 수 없으므로(HelpCatalogSO.OnValidate와 같은 이유) 여기서 검사한다.
    private void OnValidate()
    {
        _rows = null;

        HashSet<string> seenNameKeys = new();
        WarnDuplicatesAndGaps(_monsterPrefabs, seenNameKeys);
        WarnDuplicatesAndGaps(_bossPrefabs, seenNameKeys);
    }

    private void WarnDuplicatesAndGaps(List<BaseMonster> prefabs, HashSet<string> seenNameKeys)
    {
        if (prefabs == null)
        {
            return;
        }

        foreach (BaseMonster prefab in prefabs)
        {
            if (prefab == null)
            {
                Debug.LogWarning($"[MonsterCodexCatalogSO] {name}: 비어 있는 목록 칸이 있습니다.", this);
                continue;
            }

            if (prefab.Data == null)
            {
                Debug.LogWarning(
                    $"[MonsterCodexCatalogSO] {name}: {prefab.name}에 MonsterData가 배선되어 있지 않습니다.",
                    this);
                continue;
            }

            string nameKey = prefab.Data.NameLocKey;
            if (string.IsNullOrWhiteSpace(nameKey))
            {
                Debug.LogWarning(
                    $"[MonsterCodexCatalogSO] {name}: {prefab.Data.name}의 이름 키(_nameLocKey)가 비어 있어 " +
                    "해금 기록 키로 쓸 수 없습니다.",
                    this);
                continue;
            }

            if (!seenNameKeys.Add(nameKey))
            {
                Debug.LogWarning(
                    $"[MonsterCodexCatalogSO] {name}: 이름 키 '{nameKey}'가 중복됩니다({prefab.name}). " +
                    "해금 기록 키로 재사용되므로 한 항목만 남겨야 합니다.",
                    this);
            }
        }
    }
}
