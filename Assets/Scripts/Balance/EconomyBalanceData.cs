using UnityEngine;

/// <summary>
/// 자원 경제의 전역 기준 수치. 밸런싱 반복마다 코드를 고치지 않도록, 여러 시스템에 흩어져
/// 하드코딩돼 있던 상수를 한 에셋으로 모은 것이다(수치 근거는 Docs/자원밸런싱_초안.md).
///
/// 여기 모으는 기준은 "밸런싱 담당이 조정할 값"이다. 격자 기하(사분면 각도 등)처럼
/// 조정 대상이 아닌 상수는 각 클래스에 그대로 둔다.
/// </summary>
[CreateAssetMenu(
    menuName = "TowerAndDragon/Balance/Economy",
    fileName = "EB_EconomyBalance")]
public sealed class EconomyBalanceData : ScriptableObject
{
    [Header("생산")]
    [Tooltip("성 주변 셀 한 칸의 기본 생산량. 대각선·거리 보너스가 이 값 위에 더해진다.")]
    [Min(1)]
    [SerializeField] private int _baselineCellYield = 5;

    [Tooltip("대각선 방향 셀에 거리 1당 더해지는 생산량 계수. 클수록 외곽 대각선이 풍요로워진다.")]
    [Min(0f)]
    [SerializeField] private float _diagonalBonusFactor = 0.4f;

    [Header("유지비")]
    [Tooltip("인구 1명이 하루에 먹는 식량. 배치 여부와 무관하게 최대 인구 전원이 먹는다.")]
    [Min(0)]
    [SerializeField] private int _foodUpkeepPerPopulation = 1;

    [Header("철거 환급")]
    [Tooltip("낮밤 사이클이 한 번도 돌지 않은 당일 철거 시 돌려받는 건설 비용 비율.")]
    [Range(0f, 1f)]
    [SerializeField] private float _demolishRefundRatioSameDay = 1f;

    [Tooltip("하루 이상 지난 건물을 철거할 때 돌려받는 건설 비용 비율.")]
    [Range(0f, 1f)]
    [SerializeField] private float _demolishRefundRatioLate = 0.7f;

    public int BaselineCellYield => _baselineCellYield;
    public float DiagonalBonusFactor => _diagonalBonusFactor;
    public int FoodUpkeepPerPopulation => _foodUpkeepPerPopulation;
    public float DemolishRefundRatioSameDay => _demolishRefundRatioSameDay;
    public float DemolishRefundRatioLate => _demolishRefundRatioLate;
}
