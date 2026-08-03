public enum MonsterElementRule
{
    // 모든 속성 및 무속성 공격을 허용한다.
    Normal,
    // 설정한 속성의 공격만 허용한다.
    OnlyMatchingElement,
    // 설정한 속성의 공격만 차단한다.
    ImmuneToMatchingElement
}
