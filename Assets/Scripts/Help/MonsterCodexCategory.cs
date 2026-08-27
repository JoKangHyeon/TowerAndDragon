/// <summary>
/// 적 도감 목록에서 항목을 묶는 갈래. 도움말의 HelpCategory와 달리 몬스터 데이터에는
/// 저장되지 않는다 - 이 프로젝트는 보스를 별도 타입이 아니라 웨이브 번호로 판정하므로
/// (WaveCycleSnapshot.IsBossWave), "보스인가"는 도감 카탈로그가 스스로 아는 사실이다.
///
/// 직렬화되지 않는다(MonsterCodexCatalogSO가 프리팹 소속으로 그때그때 판정한다) - 값이 정수로
/// 저장되지 않으므로 HelpCategory와 달리 선언 순서를 자유롭게 바꿔도 기존 저장물이 깨지지 않는다.
/// 다만 목록 표시 순서는 이 선언 순서를 그대로 따른다.
/// </summary>
public enum MonsterCodexCategory
{
    Ground = 0,
    Air,
    Boss,
}
