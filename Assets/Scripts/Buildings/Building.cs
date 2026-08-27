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

    [Tooltip("회전 스텝(0~3, 90도 단위)별로 교체할 스프라이트. 회전해도 모양이 같은 건물은 전부 같은 스프라이트를 넣어도 된다. " +
        "비워 두면 회전해도 프리팹에 배치된 스프라이트를 그대로 쓴다(SetRotation이 null을 건너뛴다) - " +
        "유저가 회전시킬 수 없는 성, 배치 시점에 속성별 스프라이트로 갈아끼우는 새끼용 타워가 그렇다.")]
    [WiringOptional]
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

    [Tooltip("세이브가 이 건물을 되살릴 때 쓰는 안정 ID(BuildingCatalog의 키). 프리팹 이름을 바꿔도 이 값은 유지해야 한다. " +
        "비워 두면 저장 대상에서 제외된다 - 성(스스로 배치)·임시 방벽(밤에 사라짐)이 그렇다. " +
        "이미 만들어진 세이브가 있는 상태에서 값을 바꾸면 그 건물은 불러올 때 사라진다.")]
    [SerializeField]
    private string _prefabId;

    [SerializeField]
    [FormerlySerializedAs("IsMoveable")]
    private bool _isMoveable;

    [SerializeField]
    [FormerlySerializedAs("IsRemoveable")]
    private bool _isRemoveable;

    //밤에도 이동 허용 여부 (기본 false)
    public virtual bool CanMoveAtNight => false;
    
    private bool _isOpen = false; // 해금 여부
    private SpriteRenderer _spriteRenderer;
    private Color _originalColor;
    private Vector3? _placementOffset;
    private Vector3? _baseScale;
    private int _rotationSteps;
    private IBuildingMoveGrantQuery _moveGrantQuery;

    public bool IsOpen => _isOpen;
    public Sprite Sprite => _sprite;

    public string PrefabId => _prefabId;

    // 세이브가 재생성할 수 있는 건물인가. 캡처와 복원 양쪽이 쓰는 유일한 필터 기준이다 -
    // 타입별 예외 목록(성·랜드마크·방벽...)을 코드에 두지 않으려고 프리팹 데이터로 판정한다.
    public bool IsSaveable => !string.IsNullOrWhiteSpace(_prefabId);

    // 이 인스턴스가 점유한 footprint의 앵커(좌하단 원점). GridMap이 배치·재배치 때마다 채운다.
    // 점유 셀 목록에서 역산하지 않는 이유: FootprintShape에 구멍이 있으면 앵커 칸이 비어 있을 수 있어
    // min(점유 좌표)가 앵커와 어긋난다.
    public Vector3Int PlacementAnchor { get; private set; }

    // GridMap 전용. 셀 점유와 앵커가 어긋나면 세이브가 엉뚱한 자리에 건물을 되살린다.
    public void SetPlacementAnchor(Vector3Int anchor) => PlacementAnchor = anchor;

    // 범위 판정의 기준점 - 이 건물이 점유한 풋프린트의 "눈에 보이는 타일 표면" 중앙
    // (지형 고저차 포함, 셀 하이라이트·청크 경계가 그려지는 평면과 같다 - GridMap.CellSurfaceOffset).
    // GridMap.ConvertGridToWorld가 돌려주는 셀 중앙 평면과도, 프리팹 루트 localPosition만큼
    // 더 위로 띄운 "표시" 좌표 transform.position(새끼용은 1.5, 일반 타워는 1)과도 다른 평면이다 -
    // 셋 다 그대로 판정 중심에 쓰면 사거리·버프 반경 등의 원이 타일과 어긋난다.
    // GridMap이 배치·복원·이동 때마다 PlacementAnchor와 함께 채운다.
    public Vector3 GroundWorldPosition { get; private set; }

    // GridMap 전용. PlacementAnchor를 세팅할 때 반드시 같이 불러야 한다.
    public void SetGroundWorldPosition(Vector3 position) => GroundWorldPosition = position;

    // 이 건물에 마지막으로 적용된 화면 정렬 순서(IsometricMath.ComputeDepthSortOrder). 클수록 앞쪽이다.
    // 겹친 건물의 클릭 후보를 "보이는 순서"대로 줄 세울 때 쓴다(BuildingClickCycle) - 앵커로 다시
    // 계산하지 않고 실제로 적용된 값을 읽어야, 렌더 순서와 선택 순서가 갈라지지 않는다.
    public int DepthSortOrder { get; private set; }

    /// <summary>겹친 건물을 "보이는 순서"대로 줄 세우는 비교자. 화면 앞쪽(DepthSortOrder가 큰 쪽)이 먼저 온다.
    /// 클릭 순환(BuildingClickCycle)과 호버 아웃라인(BuildingHoverOutline)이 같은 규칙을 써야
    /// "눌리는 것"과 "아웃라인이 뜨는 것"이 갈라지지 않는다.
    ///
    /// 동점을 앵커 좌표로 끊는 이유: List.Sort는 불안정 정렬이라, 같은 후보 집합이 호출마다 다른 순서로
    /// 나오면 클릭 순환이 "후보가 바뀌었다"고 오판해 진행되지 않는다.</summary>
    public static int CompareFrontToBack(Building left, Building right)
    {
        int byDepth = right.DepthSortOrder.CompareTo(left.DepthSortOrder);

        if (byDepth != 0)
            return byDepth;

        int byX = left.PlacementAnchor.x.CompareTo(right.PlacementAnchor.x);
        return byX != 0 ? byX : left.PlacementAnchor.y.CompareTo(right.PlacementAnchor.y);
    }

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
    public virtual bool IsRemoveable => _isRemoveable;

    // 운영 중단 상태(얼음 새끼용 버프가 사라진 화염지대 건물 등). 인구 배치만 막는다.
    // Tower._isDisabled(체력 소진 → 부활 대기)와는 별개 개념이라 이름을 구분한다.
    public bool IsSuspended {get; private set;} 

    public void SetSuspended(bool isSuspended) => IsSuspended = isSuspended;

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

        // GridMap을 거치지 않고 씬에 직접 놓인 개체(에디터 테스트 씬 등)를 위한 기본값 -
        // GridMap.ConstructBuilding/RegisterFootprint/MoveBuilding이 배치 시 곧바로 정확한
        // 값으로 덮어쓴다.
        GroundWorldPosition = transform.position;
    }

    public void SetHighlighted(bool isHighlighted, Color highlightColor)
    {
        if (_spriteRenderer == null)
            return;

        _spriteRenderer.color = isHighlighted ? highlightColor : _originalColor;
    }

    // virtual - 본체 스프라이트 위에 자식 스프라이트를 얹는 건물(슬라임 농장의 슬라임 등)은
    // 여기서 자식들의 정렬 순서까지 같이 맞춰야 한다. 배치·세이브 복원·이동 세 경로가 모두
    // 이 메서드를 지나므로(GridMap.ConstructBuilding/RegisterFootprint/MoveBuilding),
    // 자식 정렬을 갱신할 지점은 이 하나로 충분하다.
    public virtual void SetDepthSortOrder(int sortingOrder)
    {
        // 조기 반환보다 앞에서 기록한다 - 스프라이트가 없는 건물도 클릭 후보 정렬에는 참여한다.
        DepthSortOrder = sortingOrder;

        if (_spriteRenderer == null)
            return;

        _spriteRenderer.sortingOrder = sortingOrder;
    }

    /// <summary>월드 좌표가 이 건물의 스프라이트 몸통 안인지. 클릭 판정은 기본적으로 그리드 셀 기준이라
    /// (GridMap.PickCellAtWorldPoint) 스프라이트가 자기 footprint보다 훨씬 높게 그려진 건물은
    /// 몸통을 눌러도 빈 땅으로 판정된다. 그 몸통까지 클릭 후보로 잡기 위한 판정이며
    /// (BuildingClickCycle), SpriteHoverFade와 같은 함수를 쓰므로 "반투명해진 곳 = 눌리는 곳"이 일치한다.</summary>
    public bool ContainsWorldPoint(Vector3 worldPoint) =>
        SpriteHitTest.Contains(_spriteRenderer, worldPoint);
}
