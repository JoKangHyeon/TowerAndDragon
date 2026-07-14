using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Splines;

public class WaveSpawner : MonoBehaviour 
{
    [SerializeField] private WaveData _waveData;
    [SerializeField] private SplineContainer _path;
    [SerializeField] private Transform _spawnPoint;
    [SerializeField] private Transform _mainCastle;

    private void Start()
    {
        StartWaveAsync().Forget();
    }

    public void Initialize(
        WaveData waveData,
        SplineContainer path,
        Transform spawnPoint,
        Transform mainCastle)
    {
        _waveData = waveData;
        _path = path;
        _spawnPoint = spawnPoint;
        _mainCastle = mainCastle;
    }

    //임시용 웨이브 시작ㅜ
    public async UniTask StartWaveAsync()
    {
        await SpawnWaveAsync();
    }

    private async UniTask SpawnWaveAsync()
    {
        foreach (SpawnGroup spawnGroup in _waveData.SpawnGroups)
        {
            await SpawnGroupAsync(spawnGroup);
        }
    }

    private async UniTask SpawnGroupAsync(SpawnGroup spawnGroup)
    {
        if (spawnGroup.StartDelay > 0f)
        {
            await UniTask.Delay(
                System.TimeSpan.FromSeconds(spawnGroup.StartDelay),
                cancellationToken: destroyCancellationToken);
        }

        for (int i = 0; i < spawnGroup.SpawnCount; i++)
        {
            BaseMonster monster = Instantiate(
                spawnGroup.MonsterPrefab,
                _spawnPoint.position,
                _spawnPoint.rotation,
                transform);

            monster.Setup(spawnGroup.MonsterData, _path, _mainCastle);

            bool hasNextMonster = i + 1 < spawnGroup.SpawnCount;
            if (hasNextMonster && spawnGroup.SpawnInterval > 0f)
            {
                await UniTask.Delay(
                    System.TimeSpan.FromSeconds(spawnGroup.SpawnInterval),
                    cancellationToken: destroyCancellationToken);
            }
        }
    }
}
