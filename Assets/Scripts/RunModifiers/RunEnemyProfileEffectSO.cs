using UnityEngine;

/// <summary>런 전역으로 적용할 적 강화 프로필을 지정하는 효과.
/// 기존 <see cref="EnemyEnhancementProfileSO"/>를 그대로 참조하므로 강화 규칙을 표현하는
/// 신규 코드가 0줄이다 - 지형 점령 보상으로 붙는 강화와 완전히 같은 파이프라인을 탄다.
/// 그 결과 PortalWavePreviewRenderer가 강화 수치를 자동으로 표시한다.</summary>
[CreateAssetMenu(
    menuName = "TowerAndDragon/Mutators/Effects/EnemyProfile",
    fileName = "RME_EnemyProfile")]
public sealed class RunEnemyProfileEffectSO : RunMutatorEffectSO
{
    [SerializeField]
    [Tooltip("런 전역으로 적용할 적 강화 프로필.")]
    private EnemyEnhancementProfileSO _profile;

    public override EnemyEnhancementProfileSO GetEnemyProfile()
    {
        return _profile;
    }

    /// <summary>테스트·에디터 저작 스크립트가 인스펙터 없이 값을 채울 때 쓴다.</summary>
    public void Configure(EnemyEnhancementProfileSO profile)
    {
        _profile = profile;
    }
}
