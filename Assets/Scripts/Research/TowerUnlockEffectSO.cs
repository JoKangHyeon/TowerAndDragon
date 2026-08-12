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

    public TowerData Tower => _tower;

    public override TowerData GetUnlockedTower() => _tower;
}
