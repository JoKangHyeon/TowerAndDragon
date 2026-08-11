using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 건물 하나에 붙는 월드 스페이스 효과 표식 줄. 값 주입만 받는 순수 View로,
/// 어떤 효과가 걸렸는지는 BuildingEffectBadgeSystem이 판정해 넘긴다.
///
/// UI_WorldHealthBar와 같은 "런타임 자식 부착" 뷰라 스케일 보정·Y 오프셋 처리를 그대로 따른다.
/// 다만 체력바가 스프라이트 위쪽 끝을 쓰므로, 표식은 아래쪽 끝에 놓아 서로 겹치지 않게 한다.
/// </summary>
public class UI_BuildingEffectBadge : MonoBehaviour
{
    [Tooltip("숨김 여부를 토글하는 대상 Canvas. 표식이 하나도 없을 때 이걸 끈다. Raycast를 받지 않도록 GraphicRaycaster를 붙이지 말 것.")]
    [SerializeField] private Canvas _canvas;

    [Tooltip("효과 한 칸을 그리는 슬롯 프리팹.")]
    [SerializeField] private UI_BuildingEffectIconSlot _iconSlotPrefab;

    [Tooltip("생성된 슬롯이 들어갈 부모. HorizontalLayoutGroup 권장 - 정렬은 레이아웃에 맡긴다.")]
    [SerializeField] private Transform _iconContainer;

    [Tooltip("SpriteRenderer를 찾지 못했을 때 쓰는 기본 Y 오프셋(월드 유닛). 아래쪽에 두므로 음수다.")]
    [SerializeField] private float _fallbackYOffset = -0.4f;

    [Tooltip("스프라이트 세로 중심에서 아래로 얼마나 내려갈지(반높이 기준 비율). 0이면 정중앙, 1이면 스프라이트 맨 아래.")]
    [Range(0f, 1f)]
    [SerializeField] private float _bottomAnchorRatio = 0.5f;

    [Tooltip("위에서 구한 지점에서 추가로 더 내릴 간격(월드 유닛).")]
    [SerializeField] private float _yPadding = 0.05f;

    private ComponentPool<UI_BuildingEffectIconSlot> _slotPool;

    /// <summary>부착 직후 한 번 호출해 위치와 크기를 소유 건물에 맞춘다.</summary>
    public void Bind(SpriteRenderer ownerRenderer)
    {
        ApplyScaleCompensation();
        ApplyYOffset(ownerRenderer);
        SetHidden(true);
    }

    /// <summary>
    /// 이번에 표시할 효과 목록을 그린다. 아이콘 테이블에 등록되지 않은 종류는 건너뛰므로,
    /// 아트가 아직 없는 효과가 섞여 있어도 나머지는 정상적으로 그려진다.
    /// </summary>
    public void Render(IReadOnlyList<BuildingEffectDescriptor> effects, BuildingEffectIconData iconData)
    {
        if (!EnsurePool() || iconData == null)
        {
            return;
        }

        int usedCount = 0;

        if (effects != null)
        {
            for (int i = 0; i < effects.Count; i++)
            {
                BuildingEffectDescriptor effect = effects[i];

                if (!iconData.TryResolve(effect.Kind, out BuildingEffectIconEntry entry))
                {
                    continue;
                }

                UI_BuildingEffectIconSlot slot = _slotPool.Get(usedCount);
                slot.Setup(entry.Icon, entry.IconColor, BuildingEffectFormatter.FormatBadgeLabel(effect));
                usedCount += 1;
            }
        }

        _slotPool.DeactivateFrom(usedCount);
        SetHidden(usedCount == 0);
    }

    // 풀은 런타임 생성물이라 인스펙터 배선 가드(WiringGuard)의 대상이 아니다. 프리팹 구성이
    // 빠졌을 때만 조용히 비활성 상태로 남는다.
    private bool EnsurePool()
    {
        if (_slotPool != null)
        {
            return true;
        }

        if (!WiringGuard.Require(_iconSlotPrefab, nameof(_iconSlotPrefab), this) ||
            !WiringGuard.Require(_iconContainer, nameof(_iconContainer), this))
        {
            return false;
        }

        _slotPool = new ComponentPool<UI_BuildingEffectIconSlot>(_iconSlotPrefab, _iconContainer);
        return true;
    }

    private void SetHidden(bool isHidden)
    {
        if (_canvas != null)
        {
            _canvas.enabled = !isHidden;
        }
    }

    // 건물 프리팹마다 루트 스케일이 제각각이라(0.12배~3배), 부모 스케일의 역수를 걸어
    // 모든 건물 위에서 표식이 화면상 같은 크기로 보이게 한다(UI_WorldHealthBar와 동일).
    private void ApplyScaleCompensation()
    {
        Vector3 parentLossyScale = transform.parent != null
            ? transform.parent.lossyScale
            : Vector3.one;

        transform.localScale = new Vector3(
            SafeReciprocal(parentLossyScale.x),
            SafeReciprocal(parentLossyScale.y),
            SafeReciprocal(parentLossyScale.z));
    }

    private static float SafeReciprocal(float value) =>
        Mathf.Approximately(value, 0f) ? 1f : 1f / value;

    // 건물 아래쪽에 놓는다 - 체력바가 스프라이트 위쪽 끝을 쓰므로 자리가 겹치지 않는다.
    //
    // bounds.min을 그대로 쓰지 않는 이유: 건물 스프라이트는 아래쪽에 투명 여백이 꽤 남아 있어
    // (벌목장 기준 1.3 유닛) 맨 아래에 붙이면 표식이 건물에서 뚝 떨어져 보인다. 중심에서
    // 반높이의 일정 비율만큼 내려가는 방식이라 스프라이트 크기가 달라도 비례해서 따라간다.
    private void ApplyYOffset(SpriteRenderer ownerRenderer)
    {
        Transform parent = transform.parent;
        float parentScaleY = parent != null ? parent.lossyScale.y : 1f;

        float localOffsetY;
        if (ownerRenderer != null && parent != null && !Mathf.Approximately(parentScaleY, 0f))
        {
            Bounds bounds = ownerRenderer.bounds;
            float worldAnchorY = bounds.center.y - bounds.extents.y * _bottomAnchorRatio - _yPadding;
            localOffsetY = (worldAnchorY - parent.position.y) / parentScaleY;
        }
        else
        {
            localOffsetY = Mathf.Approximately(parentScaleY, 0f)
                ? _fallbackYOffset
                : _fallbackYOffset / parentScaleY;
        }

        transform.localPosition = new Vector3(0f, localOffsetY, 0f);
    }
}
