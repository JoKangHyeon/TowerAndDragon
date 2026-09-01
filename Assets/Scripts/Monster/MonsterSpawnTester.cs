using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Splines;

/// <summary>
/// 테스트용 임시 스포너. 실제 웨이브 스포너 구현 전, 적이 스플라인 경로를
/// 제대로 따라가는지 확인하기 위한 스크립트다.
/// 인스펙터에서 프리팹/데이터/경로/메인성을 연결하면 재생 시 적을 스폰한다.
/// </summary>
public class MonsterSpawnTester : MonoBehaviour
{
    [SerializeField] private BaseMonster _monsterPrefab;
    [SerializeField] private MonsterData _data;
    [SerializeField] private SplineContainer _path;
    [SerializeField] private Transform _mainCastle;

    [Header("Spawn")]
    [SerializeField] private int _spawnCount = 1;
    [SerializeField] private float _spawnInterval = 1f;

    private void Start()
    {
        SpawnLoopAsync().Forget();
    }

    private async UniTaskVoid SpawnLoopAsync()
    {
        CancellationToken token = this.GetCancellationTokenOnDestroy();

        for (int i = 0; i < _spawnCount; i++)
        {
            SpawnOne();
            await UniTask.WaitForSeconds(_spawnInterval, cancellationToken: token);
        }
    }

    private void SpawnOne()
    {
        if (_monsterPrefab == null || _data == null)
        {
            Debug.LogWarning("MonsterSpawnTester: 프리팹 또는 데이터가 연결되지 않았습니다.");
            return;
        }

        BaseMonster monster = Instantiate(_monsterPrefab);
        monster.Setup(_data, _path, _mainCastle);
    }
}
