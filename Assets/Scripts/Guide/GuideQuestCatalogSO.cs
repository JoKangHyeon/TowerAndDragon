using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 가이드 퀘스트 전체 목록. 완료 판정(GuideQuestController)과 목록 표시(UI_GuideQuestWindow)가
/// 같은 목록을 봐야 하므로 한곳에 모은다 - 두 컴포넌트에 각자 배선하면 조용히 어긋난다
/// (HelpCatalogSO와 같은 역할이다).
///
/// 이 리스트가 이 기능의 유일한 머지 충돌 지점이다. 퀘스트 에셋을 먼저 다 만들고
/// 여기 등록은 마지막에 한 사람이 몰아서 한다.
/// </summary>
[CreateAssetMenu(menuName = "TowerAndDragon/Guide/Catalog", fileName = "GuideQuestCatalog")]
public sealed class GuideQuestCatalogSO : ScriptableObject
{
    [Tooltip("퀘스트 목록. 선언 순서는 표시 순서와 무관하다 - 표시는 일차와 지각 여부로 정렬한다.")]
    [SerializeField] private List<GuideQuestSO> _quests = new();

    [Tooltip("게임 시작 직후 1회 띄울 조언자 퀘스트. 위 목록에도 함께 넣는다.")]
    [SerializeField] private GuideQuestSO _introQuest;

    public IReadOnlyList<GuideQuestSO> Quests => _quests;
    public GuideQuestSO IntroQuest => _introQuest;

    // 중복 _questId는 퀘스트 하나만 봐서는 알 수 없으므로 목록을 든 이쪽에서 검사한다.
    // 중복되면 하나를 끝냈을 때 다른 퀘스트까지 함께 완료로 기록된다.
    private void OnValidate()
    {
        HashSet<string> seenIds = new();

        foreach (GuideQuestSO quest in _quests)
        {
            if (quest == null)
            {
                Debug.LogWarning($"[GuideQuestCatalogSO] {name}: 비어 있는 목록 칸이 있습니다.", this);
                continue;
            }

            if (string.IsNullOrWhiteSpace(quest.QuestId))
            {
                // 퀘스트 쪽 OnValidate가 이미 알리므로 여기서는 중복 검사만 건너뛴다.
                continue;
            }

            if (!seenIds.Add(quest.QuestId))
            {
                Debug.LogWarning(
                    $"[GuideQuestCatalogSO] {name}: 완료 기록 키 '{quest.QuestId}'가 중복됩니다({quest.name}).", this);
            }
        }

        if (_introQuest != null && !_quests.Contains(_introQuest))
        {
            Debug.LogWarning(
                $"[GuideQuestCatalogSO] {name}: 조언자 퀘스트가 목록에 없어 완료해도 목록에 남지 않습니다.", this);
        }
    }
}
