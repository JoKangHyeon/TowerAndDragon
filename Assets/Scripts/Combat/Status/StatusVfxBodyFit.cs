using UnityEngine;

/// <summary>
/// 상태 지속 연출을 대상 몬스터의 크기에 맞춘다. 연출 프리팹의 <b>루트</b>에 붙이고,
/// <see cref="MonsterStatusVfx"/>가 연출을 대여한 직후 <see cref="Bind"/>를 부른다.
///
/// 하는 일은 둘이다.
/// - 루트 전체를 몬스터 몸통 폭에 비례해 키운다(파티클 포함).
/// - 몸통을 덮는 그림(빙결의 얼음 껍질)이 있으면 몸통 경계에 맞춰 늘리고 정렬을 몸통에 맞춘다.
///
/// 크기를 코드에서 맞추는 이유: 몬스터 루트 스케일이 0.5~1.5로 갈려 있어 고정 크기로는 작은
/// 몬스터를 통째로 삼키거나 큰 몬스터의 머리가 삐져나온다.
/// 정렬을 베껴 오는 이유: 연출 인스턴스는 몬스터의 자식이 아니라 풀 밑에 있어
/// (<see cref="MonsterStatusVfx"/> 주석 참고) 몬스터의 정렬 순서가 저절로 따라오지 않는다.
/// </summary>
public sealed class StatusVfxBodyFit : MonoBehaviour
{
    // 몸통 경계보다 살짝 크게 감싼다 - 경계에 딱 맞추면 몸통 외곽선이 껍질 밖으로 삐져나온다.
    private const float BODY_PADDING = 1.15f;

    // 껍질의 가로·세로 배율이 벌어질 수 있는 한계. 길쭉한 몬스터에서 얼음 큐브의 윗면이 찌그러지는
    // 것을 막는다. 넘칠 때는 짧은 쪽을 키워 맞춘다 - 긴 쪽을 줄이면 몸통을 덮지 못한다.
    private const float MAX_ASPECT_STRETCH = 1.3f;

    // 껍질은 몸통과 <b>같은 정렬 슬롯</b>에 둔다. 순서를 +1 하면 한 행 앞의 몬스터들과 같은 값이 돼
    // (IsometricDepthSorter의 눈금이 행당 1이다) 그 행과 뒤섞인다.
    // 같은 슬롯 안에서는 Renderer2D의 커스텀 정렬축(0, 1, -0.46)이 순서를 정하므로, z를 조금 밀어
    // 축 위의 값을 몸통보다 낮춰(= 앞으로) 놓는다. 계수가 음수라 z를 키우는 쪽이 앞이다.
    private const float SHELL_DEPTH_OFFSET = 0.1f;

    [Tooltip("프리팹에 저장된 스케일에서 이 연출이 바닥에 그리는 폭(월드 단위). 측정해서 넣는다.")]
    [Min(0.01f)]
    [SerializeField] private float _referenceFootprintWidth = 1f;

    [WiringOptional]
    [Tooltip("몸통을 덮는 그림. 비우면 루트 크기만 맞추고 덮개 처리는 하지 않는다.")]
    [SerializeField] private SpriteRenderer _shellRenderer;

    // 프리팹에 저장된 원래 스케일. 풀에서 여러 몬스터가 돌려 쓰므로 매번 여기서 다시 계산해야 한다 -
    // 직전 대여자에게 맞춰 둔 스케일에 또 곱하면 대여할 때마다 커진다.
    private Vector3 _baseScale;

    private SpriteRenderer _bodyRenderer;

    private void Awake()
    {
        _baseScale = transform.localScale;
    }

    /// <summary>대상 몬스터의 몸통 렌더러를 물고 크기를 맞춘다. 대여 시점에 한 번만 부른다.</summary>
    public void Bind(SpriteRenderer bodyRenderer)
    {
        _bodyRenderer = bodyRenderer;

        if (bodyRenderer == null)
        {
            return;
        }

        Bounds bodyBounds = bodyRenderer.bounds;

        // 루트를 먼저 키운다 - 껍질 계산이 부모의 lossyScale을 읽으므로 순서가 뒤집히면 어긋난다.
        FitRoot(bodyBounds);
        FitShell(bodyBounds);
        ApplySorting();
    }

    // 풀에 반납되면(비활성) 물고 있던 몬스터를 놓는다 - 다음 대여자가 Bind로 다시 물린다.
    private void OnDisable()
    {
        _bodyRenderer = null;
    }

    // 몬스터가 행을 옮기면 몸통의 정렬 순서가 매 프레임 바뀐다(IsometricDepthSorter).
    // 크기는 여기서 다시 재지 않는다 - 빙결 중에도 대기 애니메이션이 돌아 경계가 흔들리므로,
    // 매 프레임 맞추면 얼음이 그 리듬으로 맥동한다.
    private void LateUpdate()
    {
        if (_bodyRenderer == null)
        {
            return;
        }

        ApplySorting();
    }

    // 바닥에 깔리는 파티클을 <b>껍질과 같은 폭</b>으로 맞춘다 - 얼음덩이 밑에서 석영이 삐져나오거나
    // 반대로 얼음이 허공에 뜬 것처럼 보이지 않게, 두 그림의 밑단 폭을 하나의 기준으로 묶는다.
    //
    // 균등하게 키운다. 세로만 눌러 낮추고 싶어도 여기서 하면 안 된다 - 파티클 메시가 X축으로 57도
    // 누워 있어(원본 팩의 기울기, 작업노트 2-2) 비균등 스케일이 기울어짐(shear)으로 나타나
    // 돌이 비스듬히 찌그러진다. 연출이 너무 높으면 프리팹에서 각 돌의 깊이를 줄인다.
    private void FitRoot(Bounds bodyBounds)
    {
        transform.localScale = _baseScale * (bodyBounds.size.x * BODY_PADDING / _referenceFootprintWidth);
    }

    // 스프라이트 원본 크기와 부모 스케일로 나눠 절대값으로 계산한다. 대여는 회전만 되돌리고
    // 스케일은 그대로 두므로(ProjectilePool.AcquirePersistentEffect), 이전 대여자에게 맞춰 둔
    // 스케일이 남아 있어도 같은 결과가 나와야 한다.
    private void FitShell(Bounds bodyBounds)
    {
        if (_shellRenderer == null || _shellRenderer.sprite == null)
        {
            return;
        }

        Transform shell = _shellRenderer.transform;
        Vector3 spriteSize = _shellRenderer.sprite.bounds.size;
        Vector3 parentScale = shell.parent != null ? shell.parent.lossyScale : Vector3.one;

        // 발밑 앵커에 놓이므로 실제로 몸통을 덮는 것은 피벗 위쪽 부분뿐이다 - 얼음 큐브의 피벗은
        // 바닥 다이아의 중심이라 스프라이트의 아래쪽 20%는 땅에 묻히는 밑동이다.
        float abovePivotRatio = 1f - _shellRenderer.sprite.pivot.y / _shellRenderer.sprite.rect.height;

        float unitWidth = spriteSize.x * parentScale.x;
        float unitHeight = spriteSize.y * parentScale.y * abovePivotRatio;

        if (Mathf.Approximately(unitWidth, 0f) || Mathf.Approximately(unitHeight, 0f))
        {
            return;
        }

        float scaleX = bodyBounds.size.x * BODY_PADDING / unitWidth;
        float scaleY = bodyBounds.size.y * BODY_PADDING / unitHeight;
        float longer = Mathf.Max(scaleX, scaleY);

        scaleX = Mathf.Max(scaleX, longer / MAX_ASPECT_STRETCH);
        scaleY = Mathf.Max(scaleY, longer / MAX_ASPECT_STRETCH);

        shell.localScale = new Vector3(scaleX, scaleY, 1f);

        // z 보정은 루트가 아니라 껍질에 건다 - 루트의 위치는 MonsterStatusVfx.Follow가 매 프레임
        // 몬스터의 z로 덮어쓴다. 부모 스케일로 나눠 월드 기준으로 항상 같은 거리가 되게 한다.
        float localDepth = Mathf.Approximately(parentScale.z, 0f)
            ? SHELL_DEPTH_OFFSET
            : SHELL_DEPTH_OFFSET / parentScale.z;

        shell.localPosition = new Vector3(0f, 0f, localDepth);
    }

    private void ApplySorting()
    {
        if (_shellRenderer == null)
        {
            return;
        }

        _shellRenderer.sortingLayerID = _bodyRenderer.sortingLayerID;
        _shellRenderer.sortingOrder = _bodyRenderer.sortingOrder;
    }
}
