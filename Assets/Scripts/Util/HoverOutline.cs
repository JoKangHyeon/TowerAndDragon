using EPOOutline;
using UnityEngine;

/// <summary>커서를 올린 오브젝트에 아웃라인을 켜고 끈다. 대상은 프리팹에 미리 심어 두지 않고
/// 처음 커서가 닿는 순간 런타임에 붙인다 - 프리팹 50여 개(타워·건물·적)를 손으로 고치지 않으려는
/// 것이고, 한 번에 하나만 켜지므로(HoverOutlineDriver) 첫 호버 한 번의 AddComponent가 전부다.
///
/// 값을 매번 전부 덮어쓰는 이유: 프리팹에 옛 Outlinable이 남아 있어도 결과가 같아야
/// "색의 출처는 드라이버의 분류표 하나"라는 말이 참이 된다.
///
/// EPO(Easy Performant Outline)를 아는 유일한 파일이다 - SpriteHitTest·WiringGuard처럼
/// 이 프로젝트의 특정 플러그인/판정 방식을 캡슐화하는 공용 유틸리티 자리에 둔다.</summary>
public static class HoverOutline
{
    // 이 프로젝트의 아웃라인 대상은 전부 스프라이트다. 파티클/메시 렌더러가 딸려 와도 무시한다.
    private const RenderersAddingMode OUTLINED_RENDERER_KINDS = RenderersAddingMode.SpriteRenderer;

    // 1이 아니면 EPO가 정보 버퍼 경로(OutlineParameters.Prepare)로 넘어가 렌더 타깃 3장과
    // 패스 한 벌을 더 쓴다. 카메라 Outliner의 blurIterations가 0이라 흐림 단계 자체가 없고
    // 셰이더의 scaler도 어느 쪽이든 1이므로, 보이는 결과는 같고 비용만 줄어든다.
    private const float NEUTRAL_SHIFT = 1f;

    public static void Show(GameObject target, Color color)
    {
        if (target == null)
        {
            return;
        }

        // 이 프로젝트의 Outlinable은(기존 4개 프리팹 기준) 전부 루트에 붙어 있었다 - 루트만 보면 된다.
        if (!target.TryGetComponent(out Outlinable outlinable))
        {
            outlinable = target.AddComponent<Outlinable>();

            // 자식까지 훑는다 - 스프라이트를 자식으로 분리한 건물(BabyDragonTower)과
            // 본체 위에 자식 스프라이트를 얹는 건물(슬라임 농장)이 있다.
            //
            // 주의: 스프라이트가 비어 있는 SpriteRenderer가 여기 딸려 오면 EPO가 렌더 패스에서
            // sprite.texture를 널 검사 없이 읽어(Outliner.UpdateSharedParameters) 매 프레임
            // 예외를 던진다. 건물·타워·몬스터 프리팹에 빈 SpriteRenderer를 자식으로 달지 말 것.
            // (체력바·효과 배지는 Canvas라 Renderer가 아니어서 여기 걸리지 않는다.)
            outlinable.AddAllChildRenderersToRenderingList(OUTLINED_RENDERER_KINDS);
        }

        outlinable.RenderStyle = RenderStyle.Single;
        outlinable.DrawingMode = OutlinableDrawingMode.Normal;
        outlinable.OutlineParameters.Enabled = true;
        outlinable.OutlineParameters.Color = color;
        outlinable.OutlineParameters.DilateShift = NEUTRAL_SHIFT;
        outlinable.OutlineParameters.BlurShift = NEUTRAL_SHIFT;

        // 켜는 것은 마지막이다 - OnEnable의 UpdateVisibility가 대상들의 화면 노출을 다시 읽어
        // EPO의 렌더 목록에 올린다. 이 프레임의 렌더링에 그대로 반영되므로 한 프레임 늦지 않는다.
        outlinable.enabled = true;
    }

    public static void Hide(GameObject target)
    {
        // Unity의 ==를 쓴다 - 그 사이 파괴된 대상(죽은 적·철거된 건물)은 널로 잡혀 넘어간다
        // (Outlinable.OnDestroy가 이미 렌더 목록에서 스스로를 뺀다).
        if (target != null && target.TryGetComponent(out Outlinable outlinable))
        {
            outlinable.enabled = false;
        }
    }
}
