using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 점령 모드에서 청크 위에 뜨는 월드 스페이스 Canvas 정보 카드.
// 지형 아이콘(Image)과 점령 시 얻는 보상(인구 + 해금 자원)을 UI 슬롯으로 표시한다.
// 배치·데이터 조회는 ChunkInfoOverlayRenderer가 하고, 이 뷰는 Setup으로 값만 채운다
// (UI_ConquestRewardSlot / UI_ConquestInfoSlot과 동일한 값-주입 패턴).
// 보상 슬롯 정렬은 컨테이너의 LayoutGroup(HorizontalLayoutGroup 등)에 맡긴다.
public class UI_ChunkInfoCard : MonoBehaviour
{
    [Tooltip("지형 아이콘 이미지.")]
    [SerializeField]
    private Image _terrainImage;

    [Tooltip("보상 슬롯 프리팹(UI_ConquestRewardSlot).")]
    [SerializeField]
    private UI_ConquestRewardSlot _rewardSlotPrefab;

    [Tooltip("생성된 보상 슬롯이 들어갈 부모(LayoutGroup 권장).")]
    [SerializeField]
    private Transform _rewardContainer;

    private ComponentPool<UI_ConquestRewardSlot> _rewardSlotPool;

    private void Awake()
    {
        if (_rewardSlotPrefab != null && _rewardContainer != null)
        {
            _rewardSlotPool = new ComponentPool<UI_ConquestRewardSlot>(_rewardSlotPrefab, _rewardContainer);
        }
    }

    // 지형 스프라이트와 보상 항목(아이콘 + 라벨) 목록을 받아 카드를 채운다.
    // 보상 슬롯은 풀로 재사용하고, 이번에 쓰지 않은 슬롯은 비활성화한다.
    public void Setup(Sprite terrainSprite, IReadOnlyList<(Sprite Icon, string Label)> rewards)
    {
        if (_terrainImage != null && terrainSprite != null)
        {
            _terrainImage.sprite = terrainSprite;
        }

        if (_rewardSlotPool == null)
        {
            return;
        }

        for (int i = 0; i < rewards.Count; i++)
        {
            UI_ConquestRewardSlot slot = _rewardSlotPool.Get(i);
            slot.Setup(rewards[i].Icon, rewards[i].Label);
        }

        _rewardSlotPool.DeactivateFrom(rewards.Count);
    }
}
