using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 도감 항목 전체 목록. 해금 판정(HelpDiscoveryController)과 목록 표시(UI_HelpWindow)가
/// 같은 목록을 봐야 하므로 한곳에 모은다 - 두 컴포넌트에 각자 배선하면 조용히 어긋난다.
/// ResearchManager가 ResearchTree 에셋 하나를 참조하는 것과 같은 역할이다.
///
/// 이 리스트가 이 기능의 유일한 머지 충돌 지점이다. 항목 에셋을 먼저 다 만들고
/// 여기 등록은 마지막에 한 사람이 몰아서 한다.
/// </summary>
[CreateAssetMenu(menuName = "TowerAndDragon/Help/Catalog", fileName = "HelpCatalog")]
public sealed class HelpCatalogSO : ScriptableObject
{
    [SerializeField] private List<HelpEntrySO> _entries = new();

    public IReadOnlyList<HelpEntrySO> Entries => _entries;

    public bool TryGet(string entryId, out HelpEntrySO entry)
    {
        foreach (HelpEntrySO candidate in _entries)
        {
            if (candidate != null && candidate.EntryId == entryId)
            {
                entry = candidate;
                return true;
            }
        }

        entry = null;
        return false;
    }

    /// <summary>
    /// 도감 목록에 보이는 항목인가. 창(UI_HelpWindow)과 HUD 배지(UI_HelpUnviewedBadge)가 서로 다른
    /// 답을 내면 "점은 있는데 볼 게 없다"가 되므로 규칙을 여기 하나만 둔다.
    ///
    /// UnlockedFromStart를 OR로 넣는 것이 핵심이다 - 튜토리얼 씬에는 HelpDiscoveryController가
    /// 없어 그 항목들이 해금 목록에 들어가지 않은 채로 목록에는 보인다.
    /// (목록을 순회하지 않으므로 static이다. 항목 하나만 보면 답이 나온다.)
    /// </summary>
    public static bool IsVisible(HelpEntrySO entry)
    {
        return entry != null && (entry.UnlockedFromStart || HelpProfile.IsUnlocked(entry.EntryId));
    }

    /// <summary>목록에 보이는데 아직 펼쳐 보지 않은 항목이 하나라도 있는가(HUD 붉은 점 판정).</summary>
    public bool HasUnviewedVisibleEntry()
    {
        foreach (HelpEntrySO entry in _entries)
        {
            if (IsVisible(entry) && !HelpProfile.IsViewed(entry.EntryId))
            {
                return true;
            }
        }

        return false;
    }

    // 중복 _entryId는 항목 하나만 봐서는 알 수 없으므로 목록을 든 이쪽에서 검사한다.
    // 중복되면 한 항목을 해금했을 때 다른 항목까지 함께 열린다.
    private void OnValidate()
    {
        HashSet<string> seenIds = new();

        foreach (HelpEntrySO entry in _entries)
        {
            if (entry == null)
            {
                Debug.LogWarning($"[HelpCatalogSO] {name}: 비어 있는 목록 칸이 있습니다.", this);
                continue;
            }

            if (string.IsNullOrWhiteSpace(entry.EntryId))
            {
                // 항목 쪽 OnValidate가 이미 알리므로 여기서는 중복 검사만 건너뛴다.
                continue;
            }

            if (!seenIds.Add(entry.EntryId))
            {
                Debug.LogWarning(
                    $"[HelpCatalogSO] {name}: 해금 키 '{entry.EntryId}'가 중복됩니다({entry.name}).", this);
            }
        }
    }
}
