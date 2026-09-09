using UnityEngine;

/// <summary>
/// [에디터 테스트 전용] PopulationManager의 생성, 배치, 회수와 최대 인구 증가를 씬에서 확인하는 테스트 컴포넌트다.
/// 실제 게임 기능이 아니라 ContextMenu와 로그를 이용한 수동 스모크 테스트에만 사용한다.
/// 본문 전체가 #if UNITY_EDITOR 안에 있어 빌드에서는 컴파일되지 않는다
/// (클래스 껍데기만 남아 씬/프리팹의 컴포넌트 참조가 Missing Script가 되지 않는다).
/// </summary>
public class PopulationDebugTester : MonoBehaviour
{
#if UNITY_EDITOR
    private const int TEST_TOWER_CAPACITY = 4;
    private const int TEST_ASSIGN_AMOUNT = 3;
    private const int TEST_UNASSIGN_AMOUNT = 1;
    private const int TEST_POPULATION_REWARD = 5;

    [SerializeField] private PopulationManager _populationManager;

    private PopulationAllocation _towerAllocation;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _populationManager.PopulationChanged.AddListener(LogPopulationState);

        bool IsCreated = _populationManager.TryCreateAllocation(
            PopulationAssignmentType.Tower,
            TEST_TOWER_CAPACITY,
            out _towerAllocation
        );

        Debug.Log($"타워 할당 생성 결과 : {IsCreated}");
        LogPopulationState(_populationManager.CurrentState);
    }

    private void OnDestroy()
    {
        if (_populationManager != null)
        {
            _populationManager.PopulationChanged.RemoveListener(LogPopulationState);
        }
    }

    [ContextMenu ("테스트/타워 인구 배치")]
    private void AssignTowerPouplation()
    {
        bool isAssigned = _populationManager.TryAssign(
            _towerAllocation,
            TEST_ASSIGN_AMOUNT
        );

        Debug.Log($"인구 배치 결과 : {isAssigned}");
    }

    [ContextMenu ("테스트/타워 인구 회수")]
    private void UnAssignTowerPopulation()
    {
        bool isUnassigned = _populationManager.TryUnassign(
            _towerAllocation,
            TEST_UNASSIGN_AMOUNT
        );

        Debug.Log ($"인구 회수 결과 : {isUnassigned}");
    }

    [ContextMenu ("테스트/최대 인구 증가")]
    private void IncreaseMaxPopulation()
    {
        bool isIncreased =
            _populationManager.TryIncreaseMaxPopulation(
                TEST_POPULATION_REWARD
            );

        Debug.Log($"최대 인구 증가 결과 : {isIncreased}");
    }

    private void LogPopulationState(PopulationState state)
    {
        Debug.Log(
            $"최대 : {state.MaxPopulation}," +
            $"할당 : {state.AssignedPopulation}," +
            $"가용 : {state.AvailablePopulation}"
        );
    }
#endif
}
