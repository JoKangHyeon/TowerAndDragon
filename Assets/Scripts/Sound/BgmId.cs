/// <summary>
/// 배경음(BGM) 식별자. 루프 재생 + 크로스페이드 대상이므로 효과음(<see cref="SoundId"/>)과 분리한다.
/// </summary>
// 카탈로그 애셋은 이 enum의 순서(정수 값)로 직렬화된다. 중간에 끼워 넣으면 이미 지정해 둔
// 클립이 조용히 다른 항목으로 밀리므로, 새 항목은 반드시 끝에 추가한다.
public enum BgmId
{
    Title,
    Day,
    Night,
    Boss,
    GameOver,
}
