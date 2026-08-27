/// <summary>
/// UI 표시용 몬스터 분류. 웨이브 예고 카드의 아이콘처럼 "기획자가 플레이어에게 보여주고 싶은
/// 분류"를 나타내며, 전투 판정에는 일절 관여하지 않는다.
///
/// 실제 이동은 프리팹에 붙은 <see cref="MonsterMovement"/> 컴포넌트가 결정하고,
/// 대공/대지 피격 판정은 <see cref="MonsterMovementType"/>이 결정한다.
/// 이 값을 그 둘의 대체로 읽으면 안 된다.
///
/// Field를 0으로 두는 이유: 이 필드가 없던 기존 애셋은 YAML에 키 자체가 없어 0으로
/// 역직렬화되는데, 기존 몬스터가 대부분 지상형이라 0이 Field여야 조용히 틀리지 않는다.
/// (TargetMovementFilter가 All을 0으로 둔 것과 같은 이유)
/// </summary>
public enum MonsterDisplayCategory
{
    Field = 0,
    Advancing = 1,
    Aerial = 2,
}
