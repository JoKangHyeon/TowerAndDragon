using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 선택한 오라 타워의 버프 연출을 씬에서 하나로 모아 그린다.
/// 소스 발밑의 범위 마커와 범위 안 수혜자들 머리 위의 구체, 둘이 짝을 이룬다.
///
/// 타워 프리팹에 컴포넌트를 붙이지 않고 여기서 모아 도는 이유는 프리팹이 12종이기 때문이다 -
/// 붙이는 방식이면 전부 손배선해야 하고, 새 타워를 추가할 때마다 잊어버릴 자리가 하나 늘어난다.
/// (몬스터 33종을 손배선 0으로 처리한 MonsterStatusVfx와 같은 판단이다.)
/// </summary>
public sealed class TowerBuffOrbCoordinator : MonoBehaviour
{
    // 오라 반경 2 안에 들어오는 칸 수다. 상한이 아니라 버퍼 초기 용량으로만 쓴다 -
    // 반경이 늘어나면 List가 알아서 커진다.
    private const int EXPECTED_RECIPIENT_COUNT = 6;

    [SerializeField] private TowerAuraSystem _towerAuraSystem;

    // BuildingPlacementController는 씬이 아니라 BuildingPlacementManager 프리팹 안에 있어
    // 씬마다 프리팹 인스턴스 내부를 배선해야 한다. 같은 참조가 필요한
    // BabyDragonRangeVfxDisplay와 같은 방식으로 런타임에 한 번 찾는다.
    private BuildingPlacementController _placementController;

    private readonly TowerBuffOrbDisplay _orbDisplay = new();
    private readonly List<Tower> _recipientBuffer = new(EXPECTED_RECIPIENT_COUNT);

    private Transform _rangeMarker;
    private GameObject _rangeMarkerPrefab;

    // 마커를 그리는 쪽이 살아 있는지 선을 숨기는 쪽(BuildingPlacementController)이 알아야 한다.
    // 이 컴포넌트가 없는 씬에서 선까지 숨기면 사거리가 아무것도 안 보인다.
    private static TowerBuffOrbCoordinator _current;

    // 플레이모드 재진입 시 Reload Domain이 꺼져 있으면 이전 세션의 참조가 정적 필드에 그대로 남는다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _current = null;
    }

    /// <summary>
    /// 이 씬에서 발밑 마커를 그려 줄 코디네이터가 살아 있는지.
    /// 마커가 대신 그려 줄 때만 사거리 선을 숨겨야 하므로 선을 그리는 쪽이 이 값을 본다.
    /// (새끼용은 여기서 제외되고 BabyDragonRangeVfxDisplay가 따로 맡는다.)
    /// </summary>
    public static bool IsDrawingMarkers => _current != null;

    private void OnEnable()
    {
        _current = this;
        WiringGuard.Require(_towerAuraSystem, nameof(_towerAuraSystem), this);
        ResolveDependencies();
    }

    private void OnDisable()
    {
        if (_current == this)
        {
            _current = null;
        }

        ReleaseAll();
    }

    private void OnDestroy()
    {
        ReleaseAll();
    }

    // 타워가 움직인 뒤(배치·이동) 위치를 읽어야 하므로 LateUpdate에서 돈다.
    private void LateUpdate()
    {
        ResolveDependencies();

        // 선택 판정은 여기 한 번뿐이다. 마커와 구체가 각각 SelectedBuilding을 따로 해석하면
        // 조건이 한 글자만 갈려도 한쪽만 뜨는 프레임이 생긴다 - 같은 결과를 나눠 쓴다.
        if (!TryResolveSelectedTower(out Tower selected))
        {
            ReleaseAll();
            return;
        }

        // 오라 타워면 발밑 마커 + 범위 안 수혜자 구체 둘 다.
        if (TowerAuraSystem.TryGetActiveAura(
                selected,
                out TowerAuraDataSO aura,
                out float auraRadius))
        {
            UpdateRangeMarker(selected, aura.RangeMarkerPrefab, auraRadius);
            UpdateRecipientOrbs(selected, aura, auraRadius);
            return;
        }

        // 오라가 없는 타워는 발밑 마커만이 기본이다. 다만 아군을 회복하는 타워는 사거리 안
        // 타워가 곧 회복 대상이라, 오라와 같은 언어로 그 위에 구체를 얹는다(생명 타워).
        UpdateAttackRangeMarker(selected);
        UpdateHealRecipientOrbs(selected);
    }

    private void ResolveDependencies()
    {
        _placementController ??=
            Object.FindFirstObjectByType<BuildingPlacementController>();
    }

    // 오라 여부는 여기서 보지 않는다 - 오라 타워와 그냥 공격 타워가 같은 창구로 들어와야
    // 마커가 한 번에 하나만 뜬다는 불변식이 유지된다.
    private bool TryResolveSelectedTower(out Tower selected)
    {
        selected = null;

        if (_placementController == null ||
            _placementController.SelectedBuilding is not Tower tower)
        {
            return false;
        }

        // 새끼용도 Tower를 물려받고 버프 모드에서 오라를 뿌리지만, 그쪽 범위 표시는
        // BabyDragonRangeVfxDisplay가 자기 프리팹으로 맡는다 - 겹쳐 그리지 않는다.
        if (tower is BabyDragonTower)
        {
            return false;
        }

        selected = tower;

        return true;
    }

    // 오라가 없는 타워의 사거리 마커. 반경은 TowerData가 아니라 실제 유효 사거리를 쓴다 -
    // ShowAttackRangeIndicatorFor가 그리는 선과 같은 값이라야 선과 파티클이 겹쳐 맞는다.
    //
    // 선(RangeIndicator)은 BuildingPlacementController가 Data.HasAttackRangeMarker를 보고
    // 숨긴다 - 마커가 선과 같은 자리라 두 겹으로 보인다. 오라 타워와 같은 판단이다.
    private void UpdateAttackRangeMarker(Tower selected)
    {
        if (selected.Data == null ||
            !selected.Data.HasAttackRangeMarker ||
            selected.Attack == null)
        {
            ReleaseRangeMarker();
            return;
        }

        UpdateRangeMarker(
            selected,
            selected.Data.AttackRangeMarkerPrefab,
            selected.Attack.EffectiveRange);
    }

    // 데이터 반경(TryGetPreviewRadius)이 아니라 유효 반경을 쓴다 - 인구가 절반인 타워에서
    // 원과 수혜자 목록이 어긋나면 플레이어가 버그로 읽는다.
    //
    // 프리팹 유무는 호출자가 아니라 여기서 본다. 마커만 꽂힌 오라와 구체만 꽂힌 오라가 각각
    // 자기 것만 그리게 하려는 것이다 - 한쪽 미배선이 다른 쪽을 죽이면 안 된다.
    private void UpdateRangeMarker(
        Tower source,
        GameObject prefab,
        float effectiveRadius)
    {
        if (prefab == null || effectiveRadius <= 0f)
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

    // 반경은 UpdateAttackRangeMarker가 쓴 것과 같은 유효 사거리다 - 마커 원과 구체 목록이
    // 다른 값을 읽으면 원 안인데 구체가 없는 타워가 생긴다.
    //
    // 구체 프리팹만이 아니라 TowerAllyHealer까지 함께 본다 - 회복하지 않는 타워에 구체 프리팹을
    // 잘못 꽂으면 "이 타워가 뭔가 해 주고 있다"는 거짓 신호가 되기 때문이다.
    private void UpdateHealRecipientOrbs(Tower selected)
    {
        if (_towerAuraSystem == null ||
            selected.Data == null ||
            !selected.Data.HasAllyHealOrb ||
            selected.Attack == null ||
            selected.GetComponent<TowerAllyHealer>() == null ||
            !CanHeal(selected))
        {
            _orbDisplay.ReleaseAll();
            return;
        }

        _towerAuraSystem.CollectHealRecipients(
            selected,
            selected.Attack.EffectiveRange,
            _recipientBuffer);

        _orbDisplay.Sync(selected.Data.AllyHealOrbPrefab, _recipientBuffer);
    }

    // 구체는 "지금 회복해 주고 있다"가 아니라 **"이 타워는 생명 타워 범위 안이라 회복을 받을 수
    // 있다"**는 표시다 - 오라 수혜자 구체와 같은 언어다. 그래서 체력이 깎였는지로 거르지 않는다
    // (CollectHealRecipients도 범위와 생사만 본다). 이 문장을 "회복 중"으로 읽고 고치지 말 것.
    //
    // 다만 소스가 회복을 못 쏘는 상태면 범위 안이어도 받을 수 있는 것이 없으므로 전부 걷는다.
    // "쏠 수 있는가"의 근거는 TowerAllyHealer.Update다 - 거기는 `_staffing != null &&
    // !CanOperate`일 때만 발사를 접으므로 ITowerStaffing이 없는 타워는 그냥 쏜다.
    // 여기서 없는 것을 "못 한다"로 읽으면 회복은 도는데 구체만 안 뜨는 타워가 생긴다.
    // (오라 타워는 TowerAuraSystem이 반대로 - 없으면 오라 없음 - 읽는다. 거기는 같은 함수가
    //  효과와 표시를 동시에 정해 어긋날 수 없지만, 여기는 효과가 healer에 있어 그쪽을 따라간다.)
    private static bool CanHeal(Tower tower)
    {
        if (tower.IsDead || tower.IsParalyzed || tower.IsReviving)
        {
            return false;
        }

        ITowerStaffing staffing = tower.GetComponent<ITowerStaffing>();

        return staffing == null || staffing.CanOperate;
    }

    private void UpdateRecipientOrbs(
        Tower source,
        TowerAuraDataSO aura,
        float effectiveRadius)
    {
        if (!aura.HasRecipientOrb || _towerAuraSystem == null)
        {
            _orbDisplay.ReleaseAll();
            return;
        }

        // 마커가 쓴 것과 같은 유효 반경을 넘긴다 - 원 안인데 구체가 안 뜨는 타워를 없애려는 것.
        // 소스 자신은 수집 루프의 source == target에서 걸러진다(예외 코드가 따로 없다).
        _towerAuraSystem.CollectAuraRecipients(
            source,
            aura,
            effectiveRadius,
            _recipientBuffer);

        _orbDisplay.Sync(aura.RecipientOrbPrefab, _recipientBuffer);
    }

    private void ReleaseAll()
    {
        ReleaseRangeMarker();
        _orbDisplay.ReleaseAll();
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
