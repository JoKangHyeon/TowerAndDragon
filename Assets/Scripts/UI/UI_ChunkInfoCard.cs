using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 점령 모드에서 청크 위에 뜨는 월드 스페이스 Canvas 정보 카드.
// 지형 아이콘(Image), 인구 보상(아이콘 + 수량), 생산 가능 자원 아이콘 목록을 표시한다.
// 배치·데이터 조회는 ChunkInfoOverlayRenderer가 하고, 이 뷰는 Setup으로 값만 채운다
// (UI_ConquestRewardSlot / UI_ConquestInfoSlot과 동일한 값-주입 패턴).
// 자원 아이콘 슬롯(Slot_ChunkIcon)은 라벨 없이 아이콘만 표시하고, "생산 가능" 문구는 프리팹에 고정되어 있다.
// 자원 아이콘 정렬은 컨테이너의 LayoutGroup(HorizontalLayoutGroup 등)에 맡긴다.
public class UI_ChunkInfoCard : MonoBehaviour
{
    [Tooltip("인구 보상 아이콘.")]
    [SerializeField]
    private Image _populationIconImage;

    [Tooltip("인구 보상 수량 텍스트(+N).")]
    [SerializeField]
    private TMP_Text _populationCountText;

    [Tooltip("생산 가능 자원 아이콘 슬롯 프리팹(Slot_ChunkIcon).")]
    [SerializeField]
    private UI_ConquestRewardSlot _resourceIconSlotPrefab;

    [Tooltip("생성된 자원 아이콘 슬롯이 들어갈 부모(LayoutGroup 권장).")]
    [SerializeField]
    private Transform _resourceIconContainer;

    private ComponentPool<UI_ConquestRewardSlot> _resourceIconSlotPool;

    private void Awake()
    {
        if (_resourceIconSlotPrefab != null && _resourceIconContainer != null)
        {
            _resourceIconSlotPool = new ComponentPool<UI_ConquestRewardSlot>(_resourceIconSlotPrefab, _resourceIconContainer);
        }
    }

    // 지형 스프라이트, 인구 보상(아이콘 + "+N" 라벨), 생산 가능 자원 아이콘(아이콘 + 틴트) 목록을 받아 카드를 채운다.
    // 자원 아이콘 슬롯은 풀로 재사용하고, 이번에 쓰지 않은 슬롯은 비활성화한다.
    public void Setup(
        Sprite terrainSprite,
        Sprite populationIcon,
        string populationLabel,
        IReadOnlyList<(Sprite Icon, Color IconColor)> resourceIcons)
    {

        if (_populationIconImage != null && populationIcon != null)
        {
            _populationIconImage.sprite = populationIcon;
        }

        if (_populationCountText != null)
        {
            _populationCountText.text = populationLabel;
        }

        if (_resourceIconSlotPool == null)
            return;

        for (int i = 0; i < resourceIcons.Count; i++)
        {
            UI_ConquestRewardSlot slot = _resourceIconSlotPool.Get(i);
            slot.Setup(resourceIcons[i].Icon, resourceIcons[i].IconColor, null);
        }

        _resourceIconSlotPool.DeactivateFrom(resourceIcons.Count);
    }
}
