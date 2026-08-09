// 용 스킬트리 노드의 슬롯 종류. 속성마다 이 8종을 각각 1개씩(랭크는 별도 노드로 체인) 갖는다.
// KinTower/KinArea 이름은 바꾸지 말 것 - KinCountGateSO,
// DragonTreeManager.UnlockedKinCount / FindTowerHitStatus가 이 두 값으로 분기한다.
public enum DragonNodeKind
{
    ActiveUnlock,   // 루트 - 액티브 스킬 해금
    ActiveUp1,      // 액티브 갈래 1
    ActiveUp2,      // 액티브 갈래 2 (심화 게이트)
    PassiveUp1,     // 패시브 갈래 1
    PassiveUp2,     // 패시브 갈래 2 (심화 게이트)
    Ultimate,       // 궁극 - 비활성 잔존
    KinTower,       // 새끼용 A(타워형)
    KinArea,        // 새끼용 B(지역형)
}