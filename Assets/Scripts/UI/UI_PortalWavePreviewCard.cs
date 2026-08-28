using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 낮에 포탈 경로 위에 뜨는 월드 스페이스 Canvas 예고 카드.
// 오늘 밤 그 포탈에서 나올 적을 표시용 분류(MonsterDisplayCategory)별로 한 줄씩,
// 줄 안에서는 적 종류마다 (아이콘 + 마릿수) 한 칸씩 나열한다.
// 어느 경로로 오는지는 구분하지 않는다 - 경로별로 줄을 나누면 같은 적이 여러 번 나와 읽기 어려워진다.
// 배치·데이터 조회는 PortalWavePreviewRenderer가 하고, 이 뷰는 Setup으로 값만 채운다
// (UI_ChunkInfoCard와 동일한 값-주입 패턴).
public class UI_PortalWavePreviewCard : MonoBehaviour
{
    // stringtable - portal_preview_count : "x{0}"
    private const string COUNT_LOC_KEY = "portal_preview_count";

    private static string CountFormat => StringTable.GetString(COUNT_LOC_KEY);

    [Tooltip("적 종류 한 칸을 그릴 슬롯 프리팹(아이콘 + 마릿수 라벨).")]
    [SerializeField]
    private UI_ConquestRewardSlot _slotPrefab;

    [Tooltip("분류별 줄 배선. 여기에 없는 분류의 적은 그릴 자리가 없어 카드에 나오지 않는다.")]
    [SerializeField]
    private List<WavePreviewCategorySection> _sections = new();

    [Tooltip("줄을 껐다 켠 뒤 카드 전체 크기를 다시 맞출 최상위 레이아웃(Panel). ContentSizeFitter를 붙여 둔다.")]
    [SerializeField]
    private RectTransform _cardRoot;

    // 줄 하나의 런타임 상태. 슬롯 풀과 툴팁 트리거를 줄마다 따로 들어야 한 줄의 칸 수가 바뀌어도
    // 다른 줄의 툴팁이 엉키지 않는다.
    private class SectionRuntime
    {
        public ComponentPool<UI_ConquestRewardSlot> Pool;
        public readonly List<UI_TooltipTrigger> TooltipTriggers = new();
        public int UsedSlotCount;

        // 배선이 빠진 줄은 풀을 만들지 못해 그리기를 건너뛴다.
        public bool IsUsable => Pool != null;

        public bool HasEntries => UsedSlotCount > 0;
    }

    // _sections와 같은 인덱스를 유지한다 - 배선이 빠진 줄도 자리를 비워 둬야 뒷줄의 풀이
    // 앞줄 자리로 밀려 엉키지 않는다.
    private readonly List<SectionRuntime> _runtimes = new();
    private readonly Dictionary<MonsterDisplayCategory, int> _sectionIndexByCategory = new();

    // 전역 강화 줄을 담는 재사용 버퍼. 툴팁 문구는 SetContent가 즉시 문자열로 만들어 가지므로
    // 칸마다 다시 채워 써도 앞 칸의 툴팁이 망가지지 않는다.
    private readonly List<MonsterStatusLine> _statusLines = new();

    private void Awake()
    {
        if (!WiringGuard.Require(_slotPrefab, nameof(_slotPrefab), this))
        {
            return;
        }

        WiringGuard.Require(_cardRoot, nameof(_cardRoot), this);

        for (int i = 0; i < _sections.Count; i++)
        {
            _runtimes.Add(CreateRuntime(_sections[i], i));
        }
    }

    private SectionRuntime CreateRuntime(WavePreviewCategorySection section, int sectionIndex)
    {
        SectionRuntime runtime = new SectionRuntime();

        if (section == null)
        {
            Debug.LogError($"[UI_PortalWavePreviewCard] {sectionIndex}번 분류 줄 배선이 비어 있습니다.", this);
            return runtime;
        }

        if (!WiringGuard.Require(section.CategoryCard, nameof(section.CategoryCard), this) ||
            !WiringGuard.Require(section.SlotContainer, nameof(section.SlotContainer), this))
        {
            return runtime;
        }

        if (_sectionIndexByCategory.ContainsKey(section.Category))
        {
            Debug.LogError(
                $"[UI_PortalWavePreviewCard] {section.Category} 분류 줄이 중복 배선되었습니다.",
                this);

            return runtime;
        }

        _sectionIndexByCategory.Add(section.Category, sectionIndex);
        runtime.Pool = new ComponentPool<UI_ConquestRewardSlot>(_slotPrefab, section.SlotContainer);
        return runtime;
    }

    // 적 종류별 (아이콘, 마릿수, 데이터, 강화) 목록을 받아 분류별 줄에 나눠 담는다.
    // 슬롯은 줄마다 풀로 재사용하고, 이번에 쓰지 않은 슬롯은 비활성화한다.
    // presenter는 칸에 마우스를 올렸을 때 설명을 그릴 표시기다. 비어 있으면 툴팁만 뜨지 않는다.
    //
    // 강화를 함께 받는 이유는 툴팁이 밤에 실제로 만날 수치를 적어야 하기 때문이다 -
    // MonsterData의 설계값만 쓰면 점령 강화가 걸린 밤에 낮에 본 숫자와 실제 적이 달라진다.
    public void Setup(
        IReadOnlyList<(MonsterIcon Icon, int Count, MonsterData Data, EnemyEnhancementSnapshot Enhancement)> entries,
        UI_TooltipPresenter presenter)
    {
        for (int i = 0; i < _runtimes.Count; i++)
        {
            _runtimes[i].UsedSlotCount = 0;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            (MonsterIcon icon, int count, MonsterData data, EnemyEnhancementSnapshot enhancement) = entries[i];

            if (data == null)
            {
                continue;
            }

            if (!TryGetRuntime(data, out SectionRuntime runtime))
            {
                continue;
            }

            int slotIndex = runtime.UsedSlotCount;
            runtime.UsedSlotCount++;

            // 몬스터 아이콘은 인게임 프리팹의 SpriteRenderer 색을 그대로 옮겨 밤에 보는 것과 같게 한다.
            UI_ConquestRewardSlot slot = runtime.Pool.Get(slotIndex);
            slot.Setup(icon.Sprite, icon.Tint, string.Format(CountFormat, count));

            SetupTooltip(runtime, slotIndex, slot, data, enhancement, presenter);
        }

        ApplySections();
    }

    private bool TryGetRuntime(MonsterData data, out SectionRuntime runtime)
    {
        runtime = null;

        if (!_sectionIndexByCategory.TryGetValue(data.DisplayCategory, out int sectionIndex))
        {
            Debug.LogError(
                $"[UI_PortalWavePreviewCard] {data.name}의 분류 {data.DisplayCategory}를 그릴 줄이 배선되지 않았습니다.",
                this);

            return false;
        }

        runtime = _runtimes[sectionIndex];
        return runtime.IsUsable;
    }

    // 오늘 나오지 않는 분류는 이름 카드와 슬롯 줄을 함께 끈다 - 이름 카드만 끄면 빈 줄이
    // 레이아웃 자리를 그대로 차지해 카드에 빈칸이 남는다.
    //
    // 레이아웃이 슬롯 -> 줄 -> 카드로 중첩되어 있어, 자동 갱신에 맡기면 카드가 줄 크기를 모르는
    // 상태로 먼저 계산되어 크기가 맞지 않는다(인스펙터에서 컴포넌트를 껐다 켜면 다시 계산되어
    // 맞는 것이 같은 증상이다). 줄부터 카드 순서로 즉시 다시 계산해 같은 프레임에 크기를 맞춘다.
    private void ApplySections()
    {
        for (int i = 0; i < _runtimes.Count; i++)
        {
            SectionRuntime runtime = _runtimes[i];

            if (!runtime.IsUsable)
            {
                continue;
            }

            WavePreviewCategorySection section = _sections[i];
            runtime.Pool.DeactivateFrom(runtime.UsedSlotCount);

            section.CategoryCard.SetActive(runtime.HasEntries);
            section.SlotContainer.gameObject.SetActive(runtime.HasEntries);

            if (runtime.HasEntries)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(section.SlotContainer);
            }
        }

        if (_cardRoot != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(_cardRoot);
        }
    }

    // 이번 갱신에서 쓰지 않은 슬롯은 비활성화되고, UI_TooltipTrigger가 OnDisable에서 스스로 툴팁을 닫는다.
    // 그래서 여기서는 쓰는 칸만 채우면 되고, 밤이 되어 카드가 통째로 꺼져도 툴팁이 남지 않는다.
    private void SetupTooltip(
        SectionRuntime runtime,
        int slotIndex,
        UI_ConquestRewardSlot slot,
        MonsterData data,
        in EnemyEnhancementSnapshot enhancement,
        UI_TooltipPresenter presenter)
    {
        while (runtime.TooltipTriggers.Count <= slotIndex)
        {
            runtime.TooltipTriggers.Add(null);
        }

        if (runtime.TooltipTriggers[slotIndex] == null)
        {
            if (!WiringGuard.RequireComponent(slot, out UI_TooltipTrigger slotTrigger, this))
            {
                return;
            }

            runtime.TooltipTriggers[slotIndex] = slotTrigger;
        }

        UI_TooltipTrigger trigger = runtime.TooltipTriggers[slotIndex];

        _statusLines.Clear();
        EnemyEnhancementStatusLines.Collect(enhancement, _statusLines);

        trigger.SetPresenter(presenter);
        trigger.SetContent(MonsterTooltipBuilder.Build(
            MonsterTooltipInput.ForPreview(data, enhancement, _statusLines)));
    }
}
