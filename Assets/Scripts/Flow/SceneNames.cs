/// <summary>
/// 씬 이름 문자열의 단일 소유자. 로컬라이즈 대상이 아니고 씬 파일명과 1:1로 묶인 값이라
/// SavePaths와 같은 방식으로 상수에 가둔다.
///
/// 튜토리얼 진입점 계약이기도 하다 - 타이틀 화면은
/// <c>SceneManager.LoadScene(SceneNames.TUTORIAL)</c> 한 줄만 부르면 되고,
/// 그 밖에 어떤 정적 상태도 세팅하지 않는다(세이브 슬롯 요청 등을 하지 않는 것이 정답이다).
/// 튜토리얼이 끝난 뒤 본게임으로 넘어가는 일은 튜토리얼 씬이 스스로 처리한다.
/// </summary>
public static class SceneNames
{
    public const string TUTORIAL = "Tutorial";

    public const string SAMPLE_GAME = "SampleScene";
}
