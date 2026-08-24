/// <summary>투사체 명중 연출을 대상의 어느 시각 기준점에 놓을지 정한다.</summary>
public enum ProjectileImpactPlacement
{
    // 기존 프리팹의 직렬화 값이 바뀌지 않도록 새 값은 항상 끝에만 추가한다.
    TargetOrigin,
    Body,
    Ground,
}
