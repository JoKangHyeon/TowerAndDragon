/// <summary>새 게임 +(뮤테이터)가 곱으로 누적되는 런 단위 배율 채널.
/// 항등원은 1이며, 선택된 뮤테이터가 없으면 모든 채널이 1이 되어 표준 모드와 완전히 같아진다.
///
/// 멤버는 <b>항상 배열 끝에만</b> 추가한다 - 이 enum은 효과 SO 에셋에 정수로 직렬화되므로
/// 중간에 끼워 넣으면 이미 저작된 에셋의 채널이 조용히 다른 채널로 바뀐다.
///
/// 설계서에 있던 ConquestCost·ResearchTierDelayCycles 채널은 만들지 않았다 - 그 채널을 쓰던
/// no_refund·delayed_wisdom 뮤테이터가 카탈로그에서 삭제됐다. 쓰지 않는 채널은
/// "언젠가 쓸 것"이 아니라 죽은 코드다.</summary>
public enum RunModifierChannel
{
    ChunkYield, // 청크 생산량 배율 (lean_harvest)
    FoodUpkeep, // 인구 1명당 식량 유지비 배율 (big_appetite)
    StartingResource, // 런 시작 시 지급 자원 배율 (empty_hands)
    TowerMaxHealth, // 타워 최대 체력 배율 (weak_ground)
    CastleMaxHealth, // 메인 성 최대 체력 배율 (last_stand)
}

/// <summary>합으로 누적되는 런 단위 정수 채널. 항등원은 0이다.
/// 배율이 아니라 "일수 +1", "횟수 제한" 같은 정수 증감이어서 곱연산 채널과 분리했다.
///
/// <see cref="RunModifierChannel"/>과 같은 이유로 멤버는 항상 배열 끝에만 추가한다.</summary>
public enum RunCounterChannel
{
    EggHatchDays, // 알 부화에 필요한 일수 증가 (heavy_gravity)
    DragonTypeChangeLimitPerCycle, // 주기당 어미용 속성 변경 허용 횟수 (sworn_element)
}

/// <summary>수치가 아니라 코드 분기 자체를 바꾸는 런 규칙. 배율·가산으로 표현할 수 없는 것만 둔다.
///
/// <see cref="RunModifierChannel"/>과 같은 이유로 멤버는 항상 배열 끝에만 추가한다
/// (비트 값이 에셋에 직렬화되므로 기존 멤버의 시프트 수를 바꿔서도 안 된다).</summary>
[System.Flags]
public enum RunRuleFlag
{
    None = 0,
    NoMorningRestore = 1 << 0, // 아침 타워 즉시 복구 없음 + 낮 동안 부활 게이지 정지 (no_morning_restore)
    Ironman = 1 << 1, // 슬롯 고정·밤 직전 자동저장·게임오버 시 세이브 삭제 (ironman)
}
