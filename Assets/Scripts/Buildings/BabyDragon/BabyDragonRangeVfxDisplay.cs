using UnityEngine;

/// <summary>
/// 선택된 새끼용의 현재 모드에 맞는 전용 범위 이펙트를 하나만 유지한다.
/// 전투 판정은 건드리지 않고 기존 유효 반경을 읽어 시각 스케일만 맞춘다.
/// </summary>
[RequireComponent(typeof(BabyDragonTower))]
public sealed class BabyDragonRangeVfxDisplay : MonoBehaviour
{
    private const string RANGE_SCALE_OBJECT_NAME = "Scale";

    private BabyDragonTower _owner;
    private BuildingPlacementController _placementController;
    private BabyDragonBuffSystem _buffSystem;

    private Transform _activeEffect;
    private GameObject _activePrefab;
    private Transform _activeRangeScale;
    private Vector3 _activeRangeBaseScale;

    private void Awake()
    {
        _owner = GetComponent<BabyDragonTower>();
    }

    private void OnEnable()
    {
        ResolveDependencies();
    }

    private void LateUpdate()
    {
        ResolveDependencies();

        if (!TryResolveDisplay(out GameObject prefab, out float radius))
        {
            ReleaseActiveEffect();
            return;
        }

        if (_activeEffect == null || _activePrefab != prefab)
        {
            ReleaseActiveEffect();
            _activeEffect = ProjectilePool.AcquirePersistent(prefab);
            _activePrefab = prefab;
            _activeRangeScale = _activeEffect != null
                ? _activeEffect.Find(RANGE_SCALE_OBJECT_NAME)
                : null;
            Transform prefabRangeScale =
                prefab.transform.Find(RANGE_SCALE_OBJECT_NAME);
            _activeRangeBaseScale = prefabRangeScale != null
                ? prefabRangeScale.localScale
                : Vector3.one;

            if (_activeRangeScale != null)
            {
                _activeRangeScale.localScale = _activeRangeBaseScale;
            }
        }

        if (_activeEffect == null)
        {
            return;
        }

        _activeEffect.position = _owner.transform.position;
        _activeEffect.rotation = Quaternion.identity;
        _activeEffect.localScale = Vector3.one;

        if (_activeRangeScale != null)
        {
            _activeRangeScale.localScale = _activeRangeBaseScale * radius;
        }
    }

    private void OnDisable()
    {
        ReleaseActiveEffect();
    }

    private void OnDestroy()
    {
        ReleaseActiveEffect();
    }

    private void ResolveDependencies()
    {
        _placementController ??=
            Object.FindFirstObjectByType<BuildingPlacementController>();
        _buffSystem ??= Object.FindFirstObjectByType<BabyDragonBuffSystem>();
    }

    private bool TryResolveDisplay(out GameObject prefab, out float radius)
    {
        prefab = null;
        radius = 0f;

        if (_owner == null ||
            _placementController == null ||
            !ReferenceEquals(_placementController.SelectedBuilding, _owner) ||
            !_owner.CanOperate ||
            _owner.IsDead ||
            _owner.IsParalyzed ||
            _owner.IsReviving)
        {
            return false;
        }

        BabyDragonData data = _owner.DragonData;

        if (data == null)
        {
            return false;
        }

        if (_owner.Mode == BabyDragonMode.Attack)
        {
            prefab = data.AttackRangeVfxPrefab;
            radius = _owner.Attack != null
                ? _owner.Attack.EffectiveRange
                : 0f;
        }
        else
        {
            prefab = data.BuffRangeVfxPrefab;
            radius = _buffSystem != null
                ? _buffSystem.GetEffectiveBuffRadius(_owner)
                : data.BuffRadius;
        }

        return prefab != null && radius > 0f;
    }

    private void ReleaseActiveEffect()
    {
        if (_activeEffect != null)
        {
            if (_activeRangeScale != null)
            {
                _activeRangeScale.localScale = _activeRangeBaseScale;
            }

            ProjectilePool.ReleasePersistent(_activeEffect);
        }

        _activeEffect = null;
        _activePrefab = null;
        _activeRangeScale = null;
        _activeRangeBaseScale = Vector3.one;
    }
}
