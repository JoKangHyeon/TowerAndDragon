using UnityEngine;

/// <summary>특정 지형의 특정 페널티를 심화시키는 효과 (harsh_winter·rockfall).
///
/// ITerrainPenaltyScaleQuery의 계약이 이미 "1보다 큰 값 = 페널티 심화"를 정의해 두었으므로,
/// 새끼용이 완화(0~1)로 쓰는 그 슬롯에 반대 방향 출처를 꽂는 것뿐이다.
/// 새 곱셈 지점을 만들지 않아 건물 효과 배지·유지비 표시가 자동으로 따라온다.</summary>
[CreateAssetMenu(
    menuName = "TowerAndDragon/Mutators/Effects/TerrainPenalty",
    fileName = "RME_TerrainPenalty")]
public sealed class RunTerrainPenaltyEffectSO : RunMutatorEffectSO
{
    [SerializeField]
    [Tooltip("페널티를 심화시킬 지형.")]
    private TerrainType _terrain;

    [SerializeField]
    [Tooltip("심화시킬 페널티의 종류.")]
    private TerrainPenaltyKind _kind;

    [SerializeField]
    [Tooltip("페널티 크기에 곱할 배율. 1보다 크면 심화, 1이면 원래대로.")]
    private float _penaltyScale = 1f;

    public override float GetTerrainPenaltyScale(TerrainType terrain, TerrainPenaltyKind kind)
    {
        if (terrain != _terrain || kind != _kind)
        {
            return base.GetTerrainPenaltyScale(terrain, kind);
        }

        return _penaltyScale;
    }

    /// <summary>테스트·에디터 저작 스크립트가 인스펙터 없이 값을 채울 때 쓴다.</summary>
    public void Configure(TerrainType terrain, TerrainPenaltyKind kind, float penaltyScale)
    {
        _terrain = terrain;
        _kind = kind;
        _penaltyScale = penaltyScale;
    }
}
