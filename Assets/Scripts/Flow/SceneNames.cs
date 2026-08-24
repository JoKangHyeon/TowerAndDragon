using UnityEngine.SceneManagement;

/// <summary>
/// 씬 이름 문자열의 단일 소유자. 로컬라이즈 대상이 아니고 씬 파일명과 1:1로 묶인 값이라
/// SavePaths와 같은 방식으로 상수에 가둔다.
///
/// 튜토리얼 진입점 계약이기도 하다 - 타이틀 화면은
/// <c>SceneManager.LoadScene(SceneNames.TUTORIAL)</c> 한 줄만 부르면 되고,
/// 그 밖에 어떤 정적 상태도 세팅하지 않는다(세이브 슬롯 요청 등을 하지 않는 것이 정답이다).
/// 튜토리얼이 끝난 뒤 본게임으로 넘어가는 일은 튜토리얼 씬이 스스로 처리한다.
///
/// 인게임에서 메인 화면으로 되돌아가는 경로(설정 창의 "메인 화면으로 돌아가기")도 여기의
/// <see cref="START"/>를 쓴다. StartScene은 빌드 인덱스 0이지만, 인덱스로 부르면 씬 목록 순서가
/// 바뀌는 순간 조용히 다른 씬이 열린다.
/// </summary>
public static class SceneNames
{
    public const string START = "StartScene";

    public const string TUTORIAL = "Tutorial";

    public const string SAMPLE_GAME = "SampleScene";

    /// <summary>
    /// 지금 열려 있는 씬이 튜토리얼인지. 씬마다 달라지는 UI 구성을 인스펙터 배선(씬별 오버라이드)으로
    /// 나누지 않기 위한 판정이다 - 씬별 오버라이드는 프리팹을 고칠 때마다 빠뜨리기 쉽고,
    /// bool은 비어 있어도 WiringChecker에 걸리지 않아 조용히 틀린다.
    /// 씬 이름 비교는 이 클래스가 단독으로 소유한다(문자열의 단일 소유자라는 이 파일의 계약).
    /// </summary>
    public static bool IsTutorialScene => SceneManager.GetActiveScene().name == TUTORIAL;
}
