using UnityEngine;

/// <summary>
/// 선택한 오라 타워의 버프 연출을 씬에서 하나로 모아 그린다.
///
/// 타워 프리팹에 컴포넌트를 붙이지 않고 여기서 모아 도는 이유는 프리팹이 12종이기 때문이다 -
/// 붙이는 방식이면 전부 손배선해야 하고, 새 타워를 추가할 때마다 잊어버릴 자리가 하나 늘어난다.
/// (몬스터 33종을 손배선 0으로 처리한 MonsterStatusVfx와 같은 판단이다.)
///
/// 지금 그리는 것은 소스 발밑의 범위 마커 하나다. 범위 안 수혜자 머리 위 구체는 다음 작업이다.
/// (Docs/버프타워_이펙트_마일스톤.md의 N5)
/// </summary>
public sealed class TowerBuffOrbCoordinator : MonoBehaviour
{
    // 수혜자 수집(N5)이 그리드를 훑어야 하므로 인스턴스가 필요하다. 마커만 그리는 지금은
    // 정적 TryGetActiveAura로 충분하지만, 배선을 두 번 하지 않으려고 미리 들고 있는다.
    [SerializeField] private TowerAuraSystem _towerAuraSystem;

    // BuildingPlacementController는 씬이 아니라 BuildingPlacementManager 프리팹 안에 있어
    // 씬마다 프리팹 인스턴스 내부를 배선해야 한다. 같은 참조가 필요한
    // BabyDragonRangeVfxDisplay와 같은 방식으로 런타임에 한 번 찾는다.
    private BuildingPlacementController _placementController;

    private Transform _rangeMarker;
    private GameObject _rangeMarkerPrefab;

    private void OnEnable()
    {
        WiringGuard.Require(_towerAuraSystem, nameof(_towerAuraSystem), this);
        ResolveDependencies();
    }

    private void OnDisable()
    {
        ReleaseRangeMarker();
    }

    private void OnDestroy()
    {
        ReleaseRangeMarker();
    }

    // 타워가 움직인 뒤(배치·이동) 위치를 읽어야 하므로 LateUpdate에서 돈다.
    private void LateUpdate()
    {
        ResolveDependencies();

        if (!TryResolveRangeMarker(
                out Tower source,
                out GameObject prefab,
                out float effectiveRadius))
        {
            ReleaseRangeMarker();
            return;
        }

        if (_rangeMarker == null || _rangeMarkerPrefab != prefab)
        {
            ReleaseRangeMarker();
            _rangeMarker = ProjectilePool.AcquirePersistent(prefab);
            _rangeMarkerPrefab = prefab;
        }

        if (_rangeMarker == null)
        {
            return;
        }

        // 프리팹 루트 스케일 1이 반경 1이다(FX_Aura_* 4종 모두 Scale 래퍼가 아이소 압축을 들고 있다).
        _rangeMarker.position = source.transform.position;
        _rangeMarker.rotation = Quaternion.identity;
        _rangeMarker.localScale = Vector3.one * effectiveRadius;
    }

    private void ResolveDependencies()
    {
        _placementController ??=
            Object.FindFirstObjectByType<BuildingPlacementController>();
    }

    // 데이터 반경(TryGetPreviewRadius)이 아니라 유효 반경을 쓴다 - 인구가 절반인 타워에서
    // 원과 수혜자 목록이 어긋나면 플레이어가 버그로 읽는다.
    private bool TryResolveRangeMarker(
        out Tower source,
        out GameObject prefab,
        out float effectiveRadius)
    {
        source = null;
        prefab = null;
        effectiveRadius = 0f;

        if (_placementController == null ||
            _placementController.SelectedBuilding is not Tower selected)
        {
            return false;
        }

        // 새끼용도 Tower를 물려받고 버프 모드에서 오라를 뿌리지만, 그쪽 범위 표시는
        // BabyDragonRangeVfxDisplay가 자기 프리팹으로 맡는다 - 겹쳐 그리지 않는다.
        if (selected is BabyDragonTower)
        {
            return false;
        }

        if (!TowerAuraSystem.TryGetActiveAura(
                selected,
                out TowerAuraDataSO aura,
                out effectiveRadius))
        {
            return false;
        }

        source = selected;
        prefab = aura.RangeMarkerPrefab;

        return prefab != null && effectiveRadius > 0f;
    }

    private void ReleaseRangeMarker()
    {
        if (_rangeMarker != null)
        {
            ProjectilePool.ReleasePersistent(_rangeMarker);
        }

        _rangeMarker = null;
        _rangeMarkerPrefab = null;
    }
}
