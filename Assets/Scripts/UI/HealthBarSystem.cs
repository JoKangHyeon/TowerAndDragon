using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 몬스터 스폰(WaveManager.MonsterSpawned)과 타워 등록(GridMap.OnBuildingAdded) 이벤트를 구독해
/// 각 개체에 월드 스페이스 체력바(UI_WorldHealthBar)를 런타임으로 부착한다.
/// 프리팹은 건드리지 않는다 - 이후 추가되는 몬스터/타워 프리팹에도 자동 적용된다.
/// 성(Castle)은 UI_CastleHealth가 이미 담당하므로 제외한다.
/// </summary>
public class HealthBarSystem : MonoBehaviour
{
    [SerializeField] private UI_WorldHealthBar _barPrefab;
    [SerializeField] private WaveManager _waveManager;
    [SerializeField] private GridMap _gridMap;

    private void OnEnable()
    {
        if (WiringGuard.Require(_waveManager, nameof(_waveManager), this))
        {
            _waveManager.MonsterSpawned.AddListener(HandleMonsterSpawned);
        }

        if (WiringGuard.Require(_gridMap, nameof(_gridMap), this))
        {
            _gridMap.OnBuildingAdded.AddListener(HandleBuildingAdded);
        }
    }

    private void OnDisable()
    {
        if (_waveManager != null)
        {
            _waveManager.MonsterSpawned.RemoveListener(HandleMonsterSpawned);
        }

        if (_gridMap != null)
        {
            _gridMap.OnBuildingAdded.RemoveListener(HandleBuildingAdded);
        }
    }

    private void Start()
    {
        AttachToExistingTowersAsync(this.GetCancellationTokenOnDestroy()).Forget();
    }

    // 이 시스템이 구독을 시작하기 전에 이미 GridMap에 등록된 타워가 있을 수 있다.
    // 모든 오브젝트의 Start가 끝난 뒤(한 프레임 지연) 누락분을 훑어 부착한다.
    private async UniTaskVoid AttachToExistingTowersAsync(CancellationToken token)
    {
        await UniTask.Yield(token);

        if (!WiringGuard.Require(_gridMap, nameof(_gridMap), this))
        {
            return;
        }

        foreach (Building building in _gridMap.Buildings)
        {
            if (building is Tower || building is StoneBarricade)
            {
                Attach(building.gameObject);
            }
        }
    }

    private void HandleMonsterSpawned(BaseMonster monster)
    {
        if (monster != null)
        {
            Attach(monster.gameObject);
        }
    }

    private void HandleBuildingAdded(Building building)
    {
        if (building is Tower || building is StoneBarricade)
        {
            Attach(building.gameObject);
        }
    }

    private void Attach(GameObject entity)
    {
        if (!WiringGuard.Require(_barPrefab, nameof(_barPrefab), this))
        {
            return;
        }

        if (entity == null)
        {
            return;
        }

        Health health = entity.GetComponent<Health>();
        if (health == null)
        {
            return;
        }

        // 건물 이동(BuildingMoveGrantSystem) 등으로 재등록되면 이미 붙어 있을 수 있다 - 중복 부착 방지.
        UI_WorldHealthBar bar = entity.GetComponentInChildren<UI_WorldHealthBar>();
        if (bar == null)
        {
            bar = Instantiate(_barPrefab, entity.transform);
        }

        IReviveProgress reviveSource = entity.GetComponent<IReviveProgress>();
        IShieldInfo shieldSource = entity.GetComponent<IShieldInfo>();
        SpriteRenderer ownerRenderer = entity.GetComponentInChildren<SpriteRenderer>();
        bar.Bind(health, reviveSource, shieldSource, ownerRenderer);
    }
}
