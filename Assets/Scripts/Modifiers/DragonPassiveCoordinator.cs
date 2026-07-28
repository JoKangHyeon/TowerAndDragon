using UnityEngine;

// 불 패시브(모든 적에게 화상)를 스폰 시점에 적용한다. 속성 변경은 낮에만 가능하므로
// 밤 중 재평가는 필요 없다 - 스폰 시점의 활성 속성만 확인하면 된다.
public sealed class DragonPassiveCoordinator : MonoBehaviour
{
    [SerializeField] private DragonTreeManager _dragonTreeManager;
    [SerializeField] private WaveManager _waveManager;

    private void OnEnable()
    {
        if (_waveManager != null)
        {
            _waveManager.MonsterSpawned.AddListener(HandleMonsterSpawned);
        }
    }

    private void OnDisable()
    {
        if (_waveManager != null)
        {
            _waveManager.MonsterSpawned.RemoveListener(HandleMonsterSpawned);
        }
    }

    private void HandleMonsterSpawned(BaseMonster monster)
    {
        if (_dragonTreeManager == null || monster == null)
        {
            return;
        }

        DragonType? active = _dragonTreeManager.ActiveAttribute;

        if (!active.HasValue)
        {
            return;
        }

        StatusEffectSO status = _dragonTreeManager.GetSpawnStatus(active.Value);

        if (status != null)
        {
            monster.ApplyStatus(status);
        }
    }
}
