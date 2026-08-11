using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 낮에 포탈 경로 위에 뜨는 월드 스페이스 Canvas 예고 카드.
// 오늘 밤 그 포탈에서 나올 적을 종류별로 (아이콘 + 마릿수) 한 칸씩 나열한다.
// 어느 경로로 오는지는 구분하지 않는다 - 경로별로 줄을 나누면 같은 적이 여러 번 나와 읽기 어려워진다.
// 배치·데이터 조회는 PortalWavePreviewRenderer가 하고, 이 뷰는 Setup으로 값만 채운다
// (UI_ChunkInfoCard와 동일한 값-주입 패턴).
// 슬롯 정렬은 컨테이너의 LayoutGroup(HorizontalLayoutGroup 등)에 맡긴다.
public class UI_PortalWavePreviewCard : MonoBehaviour
{
    // stringtable - portal_preview_count : "x{0}"
    private const string COUNT_LOC_KEY = "portal_preview_count";

    private static string CountFormat => StringTable.GetString(COUNT_LOC_KEY);

    [Tooltip("적 종류 한 칸을 그릴 슬롯 프리팹(아이콘 + 마릿수 라벨).")]
    [SerializeField]
    private UI_ConquestRewardSlot _slotPrefab;

    [Tooltip("생성된 슬롯이 들어갈 부모(LayoutGroup 권장).")]
    [SerializeField]
    private Transform _slotContainer;

    private ComponentPool<UI_ConquestRewardSlot> _slotPool;
    private RectTransform _slotContainerRect;

    // 슬롯 풀과 같은 인덱스로 자라는 툴팁 트리거 목록. 풀이 슬롯을 필요할 때 만들므로
    // Awake에서 한 번에 모을 수 없어, 슬롯을 처음 꺼낼 때 채운다.
    private readonly List<UI_TooltipTrigger> _slotTooltipTriggers = new();

    private void Awake()
    {
        if (_slotPrefab != null && _slotContainer != null)
        {
            _slotPool = new ComponentPool<UI_ConquestRewardSlot>(_slotPrefab, _slotContainer);
            _slotContainerRect = _slotContainer as RectTransform;
        }
    }

    // 적 종류별 (아이콘, 마릿수, 데이터) 목록을 받아 슬롯을 채운다.
    // 슬롯은 풀로 재사용하고, 이번에 쓰지 않은 슬롯은 비활성화한다.
    // presenter는 칸에 마우스를 올렸을 때 설명을 그릴 표시기다. 비어 있으면 툴팁만 뜨지 않는다.
    public void Setup(
        IReadOnlyList<(Sprite Icon, int Count, MonsterData Data)> entries,
        UI_TooltipPresenter presenter)
    {
        if (_slotPool == null)
        {
            return;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            (Sprite icon, int count, MonsterData data) = entries[i];

            // 몬스터 아이콘은 인게임 스프라이트를 그대로 쓰므로 틴트 없이 원색으로 표시한다.
            UI_ConquestRewardSlot slot = _slotPool.Get(i);
            slot.Setup(icon, Color.white, string.Format(CountFormat, count));

            SetupTooltip(i, slot, data, presenter);
        }

        _slotPool.DeactivateFrom(entries.Count);

        // ContentSizeFitter가 슬롯 -> 패널로 중첩되어 있어, 자동 갱신에 맡기면 패널이 슬롯 크기를 모르는
        // 상태로 먼저 계산되어 가로폭이 늘어나지 않는다(인스펙터에서 컴포넌트를 껐다 켜면 다시 계산되어
        // 늘어나는 것이 같은 증상이다). 즉시 다시 계산해 같은 프레임에 폭을 맞춘다.
        if (_slotContainerRect != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(_slotContainerRect);
        }
    }

    // 이번 갱신에서 쓰지 않은 슬롯은 비활성화되고, UI_TooltipTrigger가 OnDisable에서 스스로 툴팁을 닫는다.
    // 그래서 여기서는 쓰는 칸만 채우면 되고, 밤이 되어 카드가 통째로 꺼져도 툴팁이 남지 않는다.
    private void SetupTooltip(
        int slotIndex,
        UI_ConquestRewardSlot slot,
        MonsterData data,
        UI_TooltipPresenter presenter)
    {
        while (_slotTooltipTriggers.Count <= slotIndex)
        {
            _slotTooltipTriggers.Add(null);
        }

        if (_slotTooltipTriggers[slotIndex] == null)
        {
            if (!WiringGuard.RequireComponent(slot, out UI_TooltipTrigger slotTrigger, this))
            {
                return;
            }

            _slotTooltipTriggers[slotIndex] = slotTrigger;
        }

        UI_TooltipTrigger trigger = _slotTooltipTriggers[slotIndex];

        trigger.SetPresenter(presenter);
        trigger.SetContent(MonsterTooltipBuilder.Build(data));
    }
}
