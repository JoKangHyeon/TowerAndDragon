using UnityEngine;

// 타워 해금 효과. ResearchManager.IsTowerUnlocked가 GetUnlockedTower()를 통해 이 값을 pull한다.
// ActiveSkillUnlockEffectSO(용 스킬)와 같은 형태.
//
// 해금 대상은 id가 아니라 TowerData 참조로 지목한다 - 해금 상태는 완료 노드 집합에서 파생되므로
// 따로 저장할 필요가 없고, 저장하지 않으니 안정 문자열 id도 필요 없다.
[CreateAssetMenu(
    menuName = "TowerAndDragon/Research/Effects/Tower Unlock",
    fileName = "TowerUnlockEffect")]
public sealed class TowerUnlockEffectSO : ResearchEffectSO
{
    [SerializeField] private TowerData _tower;

    [Tooltip("이 유적을 점령해야 해금된다. 비우면 연구 완료만으로 해금된다.")]
    [SerializeField] private LandmarkDataSO _requiredLandmark;

    public TowerData Tower => _tower;

    // 유적 조건을 노드(ResearchNodeData.RequiredLandmark)가 아니라 효과에 두는 이유:
    // 역설계 연구 하나가 특수 타워 네 종을 해금하는데 타워마다 점령해야 할 유적이 다르다.
    // 노드에 두면 유적별로 노드를 쪼개야 하고 "역설계 연구는 하나"라는 기획이 깨진다.
    // 효과에 두면 "노드 완료(연구) AND 유적 점령"이 저절로 AND로 성립한다 -
    // 효과가 활성 목록에 오르는 것 자체가 노드 완료를 뜻하기 때문이다.
    public LandmarkDataSO RequiredLandmark => _requiredLandmark;

    public override TowerData GetUnlockedTower() => _tower;
}
