/// <summary>
/// 성이 지금 피해를 받아도 되는지 묻는다.
///
/// 본게임은 이 질의를 배선하지 않아 언제나 피해를 받는다(기존 동작).
/// 튜토리얼만 배선한다 - 마지막 밤의 연출된 패배 전에는 성이 무너지면 안 된다.
/// 안내가 끝나기도 전에 게임오버가 나면 엔딩이 재생되고 본게임으로 넘어가 버린다.
/// CycleManager.DayEndBlockQuery와 같은 주입 방식.
/// </summary>
public interface ICastleDamageBlockQuery
{
    /// <summary>
    /// 피해량을 함께 받는다 - 막는 것은 "성을 무너뜨릴 마지막 한 방"뿐이어야 하기 때문이다.
    /// 평범한 피해까지 막으면 몬스터는 성을 계속 때리는데 성은 줄지 않아, 잡을 타워가 없을 때
    /// 밤이 끝나지 않고 교착에 빠진다.
    /// </summary>
    bool CanTakeDamage(float amount);
}
