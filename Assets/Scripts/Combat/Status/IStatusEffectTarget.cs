// 상태이상(슬로우·화상 등)을 받을 수 있는 대상의 계약. IDamageable을 넓히지 않는 이유는
// 성·건물 등 상태이상과 무관한 IDamageable까지 억지로 구현하게 만들지 않기 위함이다 -
// 몬스터만 이 인터페이스를 구현하고, 효과 적용부는 캐스팅 실패 시 조용히 no-op한다.
public interface IStatusEffectTarget
{
    void ApplyStatus(StatusEffectSO status);
    void ApplyStatus(StatusEffectSO status, DragonType? attackElement);
}
