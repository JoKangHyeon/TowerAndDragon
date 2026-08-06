using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class Building : MonoBehaviour
{
    public const int ROTATION_STEP_COUNT = FootprintShape.ROTATION_STEP_COUNT;

    [SerializeField]
    private Sprite _sprite;

    [Tooltip("회전 0(기본 방향) 기준 원본 모양. 실제 판정/표시에는 여기에 현재 회전 스텝을 적용한 값(FootprintShape 프로퍼티)이 쓰인다.")]
    [SerializeField]
    private FootprintShape _footprintShape;

    [Tooltip("회전 스텝(0~3, 90도 단위)별로 교체할 스프라이트. 회전해도 모양이 같은 건물은 전부 같은 스프라이트를 넣어도 된다.")]
    [SerializeField]
    private Sprite[] _rotationSprites = new Sprite[ROTATION_STEP_COUNT];

    [Tooltip("회전 스텝별 시각 미세조정 오프셋(선택 사항). 가로/세로 짝홀이 다른 모양의 자동 보정(GridMap.ComputeRotationCompensation)에 추가로 더할 값 - 스프라이트 피벗이 중앙이 아닌 경우 등에만 채운다. 기본값 0으로 안전.")]
    [SerializeField]
    private Vector3[] _rotationOffsets = new Vector3[ROTATION_STEP_COUNT];

    [Tooltip("회전 스텝별 시각 미세조정 스케일 배율(선택 사항). 회전마다 다르게 그려진 스프라이트의 크기가 서로 안 맞을 때만 채운다. 기본값 (1,1,1)로 안전.")]
    [SerializeField]
    private Vector3[] _rotationScales = CreateDefaultRotationScales();

    private static Vector3[] CreateDefaultRotationScales()
    {
        var scales = new Vector3[ROTATION_STEP_COUNT];
        for (int i = 0; i < scales.Length; i++)
            scales[i] = Vector3.one;
        return scales;
    }

    [SerializeField]
    [FormerlySerializedAs("IsMoveable")]
    private bool _isMoveable;

    [SerializeField]
    [FormerlySerializedAs("IsRemoveable")]
    private bool _isRemoveable;

    private bool _isOpen = false; // 해금 여부
    private SpriteRenderer _spriteRenderer;
    private Color _originalColor;
    private Vector3? _placementOffset;
    private Vector3? _baseScale;
    private int _rotationSteps;
    private IBuildingMoveGrantQuery _moveGrantQuery;

    public bool IsOpen => _isOpen;
    public Sprite Sprite => _sprite;

    // 원본(회전 0도) 모양 - 회전 계산의 기준이 된다.
    public FootprintShape BaseFootprintShape => _footprintShape;

    // 현재 회전이 적용된 모양 - 기존 호출부(GridMap 등)는 이 프로퍼티만 보면 회전을 그대로 반영한다.
    public FootprintShape FootprintShape => _footprintShape.Rotated(_rotationSteps);

    public int RotationSteps => _rotationSteps;

    // 연구 기반 일일 이동 예산의 적용 대상인지. 새끼용처럼 '건물'이 아닌 설치물은 false로
    // override해 예산과 무관하게(단, 서브클래스가 정한 별도 조건 하에) 이동한다.
    protected virtual bool UsesMoveGrant => true;

    // _isMoveable은 "이동 가능한 종류인가"를 뜻한다. 실제 이동 가부는 연구 기반 일일 예산과
    // AND로 판정한다 - 쿼리가 배선되지 않은 씬(팀원 테스트 씬 등)은 무제한 이동을 유지한다.
    // 예산 대상이 아닌 건물(UsesMoveGrant == false)은 예산 확인 자체를 건너뛴다.
    public virtual bool IsMoveable =>
        _isMoveable && (!UsesMoveGrant || _moveGrantQuery == null || _moveGrantQuery.HasRemainingMoveGrant);
    public bool IsRemoveable => _isRemoveable;

    public void SetMoveGrantQuery(IBuildingMoveGrantQuery moveGrantQuery) =>
        _moveGrantQuery = moveGrantQuery;

    // 실제 이동에 성공했을 때만 호출한다 - 취소·실패한 시도는 예산을 소비하지 않는다.
    // 예산 대상이 아닌 건물은 소비할 예산 자체가 없으므로 호출을 건너뛴다.
    public void NotifyMoved()
    {
        if (UsesMoveGrant)
            _moveGrantQuery?.ConsumeMoveGrant();
    }

    // 건설된 시점의 주기(CycleManager.CurrentCycleNumber) - 철거 시 당일 건설 여부 판정에 쓰인다.
    public int ConstructedCycle { get; private set; }

    public void SetConstructedCycle(int cycle) => ConstructedCycle = cycle;

    // 건설 비용 - 서브클래스가 자신의 Data 에셋(TowerData/ResourceProductionData 등)에서 override해 제공한다.
    // 비용이 없는 건물(Castle 등 플레이어가 짓지 않는 건물)은 기본값(빈 배열)을 그대로 쓴다.
    public virtual IReadOnlyList<ResourceAmount> BuildCost => System.Array.Empty<ResourceAmount>();

    // 배치 가능 최대 인구 - BuildCost와 동일한 패턴으로 서브클래스가 자신의 Data 에셋에서 override해 제공한다.
    public virtual int PopulationCapacity => 0;

    // 배치/재배치 시 footprint 중심에 더할 오프셋 - 재배치시 localposition 더해줄 때 누적됨 방지
    public Vector3 PlacementOffset => ComputePlacementOffset(_rotationSteps);

    // 지정한 회전 스텝 기준으로 오프셋을 미리 계산 - 아직 그 회전이 적용되지 않은 상태(미리보기 중인 프리팹 등)에서도 조회 가능.
    // GridMap.ComputeRotationCompensation(자동 계산)과는 별개로, 여기 더해지는 건 수동 미세조정분(_rotationOffsets)뿐이다.
    public Vector3 ComputePlacementOffset(int rotationSteps) =>
        (_placementOffset ?? transform.localPosition) + ResolveRotationOffset(rotationSteps);

    public void SetPlacementOffset(Vector3 offset) => _placementOffset = offset;

    // 회전 스케일 계산의 기준이 되는 원본(회전 0도) 로컬 스케일 - 아직 SetBaseScale이 호출되지 않은 경우
    // (배치 미리보기 중인 프리팹 참조 등, Instantiate/Awake를 거치지 않은 상태) 현재 transform.localScale을 그대로 기준으로 쓴다.
    public Vector3 BaseLocalScale => _baseScale ?? transform.localScale;

    public void SetBaseScale(Vector3 scale) => _baseScale = scale;

    // 지정한 회전 스텝의 스프라이트/미세조정 오프셋을 조회 - 프리팹 에셋을 직접 변경하지 않고도(미리보기용) 값을 읽을 수 있다.
    public Sprite ResolveRotationSprite(int rotationSteps) =>
        _rotationSprites != null && rotationSteps >= 0 && rotationSteps < _rotationSprites.Length
            ? _rotationSprites[rotationSteps]
            : null;

    public Vector3 ResolveRotationOffset(int rotationSteps) =>
        _rotationOffsets != null && rotationSteps >= 0 && rotationSteps < _rotationOffsets.Length
            ? _rotationOffsets[rotationSteps]
            : Vector3.zero;

    public Vector3 ResolveRotationScale(int rotationSteps) =>
        _rotationScales != null && rotationSteps >= 0 && rotationSteps < _rotationScales.Length
            ? _rotationScales[rotationSteps]
            : Vector3.one;

    // 이 인스턴스의 회전 상태를 바꾼다 - 프리팹 에셋에는 절대 호출하지 말 것(Instantiate로 만든 클론에만 호출).
    public void SetRotation(int rotationSteps)
    {
        _rotationSteps = ((rotationSteps % ROTATION_STEP_COUNT) + ROTATION_STEP_COUNT) % ROTATION_STEP_COUNT;

        Sprite sprite = ResolveRotationSprite(_rotationSteps);
        if (_spriteRenderer != null && sprite != null)
            _spriteRenderer.sprite = sprite;

        transform.localScale = Vector3.Scale(BaseLocalScale, ResolveRotationScale(_rotationSteps));
    }

    // Castle/Tower가 각자 Awake에서 자기 컴포넌트를 캐싱해야 해서 override로 재정의한다 -
    // virtual이 아니면 파생 클래스의 private Awake가 이 메서드를 가려 전혀 호출되지 않는다.
    protected virtual void Awake()
    {
        // 자식에서도 찾는다 - 스프라이트만 별도 자식으로 분리해 시각적 위치를 콜라이더/판정
        // 기준점(이 오브젝트의 transform)과 독립적으로 조정하는 건물(예: BabyDragonTower)을 지원하기 위함.
        // GetComponentInChildren은 자기 자신을 먼저 확인하므로, 루트에 바로 붙은 기존 건물들은
        // 동작 변화가 없다(Castle/Garrison/ResearchLab/Tower 전부 루트에 직접 붙어있음, 확인 완료).
        _spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (_spriteRenderer != null)
            _originalColor = _spriteRenderer.color;
    }

    public void SetHighlighted(bool isHighlighted, Color highlightColor)
    {
        if (_spriteRenderer == null)
            return;

        _spriteRenderer.color = isHighlighted ? highlightColor : _originalColor;
    }

    public void SetDepthSortOrder(int sortingOrder)
    {
        if (_spriteRenderer == null)
            return;

        _spriteRenderer.sortingOrder = sortingOrder;
    }

    /// <summary>월드 좌표가 이 건물의 스프라이트 안인지. 클릭 판정은 기본적으로 그리드 셀 기준이라
    /// (GridMap.PickCellAtWorldPoint) 스프라이트가 자기 footprint보다 훨씬 높게 그려진 건물은
    /// 몸통을 눌러도 빈 땅으로 판정된다. 그때 쓰는 보조 판정이며, SpriteHoverFade와 같은 기준이라
    /// "반투명해진 곳 = 눌리는 곳"이 일치한다.</summary>
    public bool ContainsWorldPoint(Vector3 worldPoint)
    {
        if (_spriteRenderer == null)
            return false;

        Bounds bounds = _spriteRenderer.bounds;
        worldPoint.z = bounds.center.z;

        return bounds.Contains(worldPoint);
    }
}
