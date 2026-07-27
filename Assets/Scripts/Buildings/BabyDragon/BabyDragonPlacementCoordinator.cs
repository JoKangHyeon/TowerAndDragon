using UnityEngine;

/// <summary>
/// 인벤토리(RunData.BabyDragons)의 새끼용 레코드와 그리드 위 BabyDragonTower 인스턴스를 연결한다.
/// 배치 시 속성별 BabyDragonData를 주입하고, 철거 시 인벤토리로 되돌린다.
/// 이동(GridMap.MoveBuilding)은 같은 인스턴스를 옮길 뿐 OnBuildingAdded/OnBuildingRemoving을
/// 발행하지 않으므로(GridMap.cs:673-717) 이 코디네이터가 이동을 철거로 오인하지 않는다.
/// </summary>
public class BabyDragonPlacementCoordinator : MonoBehaviour
{
    [SerializeField] private GridMap _gridMap;
    [SerializeField] private GameManager _gameManager;
    [SerializeField] private BuildingPlacementController _placementController;
    [SerializeField] private BabyDragonDataCatalog _dataCatalog;
    [SerializeField] private BabyDragonTower _babyDragonPrefab;

    private BabyDragon _pendingRecord;

    private void OnEnable()
    {
        if (_gridMap != null)
        {
            _gridMap.OnBuildingAdded.AddListener(HandleBuildingAdded);
            _gridMap.OnBuildingRemoving.AddListener(HandleBuildingRemoving);
        }
    }

    private void OnDisable()
    {
        if (_gridMap != null)
        {
            _gridMap.OnBuildingAdded.RemoveListener(HandleBuildingAdded);
            _gridMap.OnBuildingRemoving.RemoveListener(HandleBuildingRemoving);
        }
    }

    // 정식 인벤토리 UI가 생기면 그 UI가 이 메서드를 호출한다. 이번 배치는 디버그 GUI가 대신 호출한다.
    public void BeginPlacement(BabyDragon record)
    {
        if (record == null || _placementController == null || _babyDragonPrefab == null)
        {
            return;
        }

        _pendingRecord = record;
        _placementController.SelectBuilding(_babyDragonPrefab);

        // 프리팹 자체엔 스프라이트가 없어 고스트가 기본적으로 안 보이므로, 배치 시점에 실제
        // 적용될 속성 스프라이트를 미리 알아내 고스트에도 반영한다(HandleBuildingAdded의
        // ApplySprite와 같은 데이터 소스). SelectBuilding 호출 뒤에 불러야 오버라이드가 안 풀린다.
        if (_dataCatalog != null && _dataCatalog.TryResolve(record.DragonType, out BabyDragonData data))
        {
            _placementController.SetGhostSpriteOverride(data.Sprite);
        }
    }

    private void HandleBuildingAdded(Building building)
    {
        if (!(building is BabyDragonTower babyDragonTower))
        {
            // 대기 중인 레코드와 무관한 건설이 이뤄졌다(취소 후 다른 건물 배치 등) -
            // 오래된 레코드를 계속 들고 있지 않는다. BuildingPlacementController에
            // 명시적 취소 이벤트가 없어 이 방식으로 방어한다.
            _pendingRecord = null;
            return;
        }

        if (_pendingRecord == null)
        {
            return;
        }

        if (!_dataCatalog.TryResolve(_pendingRecord.DragonType, out BabyDragonData data))
        {
            Debug.LogError(
                $"[BabyDragonPlacementCoordinator] 속성 {_pendingRecord.DragonType}에 대응하는 BabyDragonData가 카탈로그에 없습니다.",
                babyDragonTower);
            _pendingRecord = null;
            return;
        }

        // OnBuildingAdded는 Instantiate 직후 같은 프레임에 동기 발행되고(GridMap.cs:430),
        // Tower.Start()는 다음 프레임에 실행되므로 여기서 먼저 Setup을 호출해도 안전하다 -
        // Start()의 `if (!_isInitialized && _towerData != null)` 가드가 중복 호출을 막는다.
        babyDragonTower.Setup(data);
        ApplySprite(babyDragonTower, data);

        _gameManager.CurrentRun.BabyDragons.Remove(_pendingRecord);
        _pendingRecord = null;
    }

    // 5속성이 프리팹 1개를 공유하므로, 배치 시점에 속성별 스프라이트로 갈아끼운다.
    // Building.SetRotation()이 이미 이 시점 이전(GridMap.ConstructBuilding)에 실행되지만
    // 이 프리팹은 _rotationSprites를 채우지 않으므로 덮어쓰이지 않는다.
    private void ApplySprite(BabyDragonTower babyDragonTower, BabyDragonData data)
    {
        if (data.Sprite == null)
        {
            return;
        }

        // 스프라이트가 자식 오브젝트로 분리되어 있을 수 있다(시각 위치를 콜라이더/판정 기준점과
        // 독립적으로 조정하기 위함) - 루트에 바로 붙어있어도 GetComponentInChildren이 그대로 찾는다.
        SpriteRenderer spriteRenderer = babyDragonTower.GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            spriteRenderer.sprite = data.Sprite;
        }
    }

    private void HandleBuildingRemoving(Building building)
    {
        if (!(building is BabyDragonTower babyDragonTower) || babyDragonTower.DragonData == null)
        {
            return;
        }

        _gameManager.CurrentRun.BabyDragons.Add(new BabyDragon
        {
            DragonType = babyDragonTower.DragonData.DragonType,
            IsInTower = false,
        });
    }
}
