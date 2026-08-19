/// <summary>
/// 확인창(<see cref="UI_ConfirmPopup"/>)에 들어갈 문구 한 줄. 스트링테이블 key와 서식 인자만 담는다.
///
/// 완성된 문자열이 아니라 key를 들고 다니는 이유: 확인창은 떠 있는 동안 언어 전환
/// (StringTable.OnLanguageChanged)에 스스로 반응해 문구를 다시 만든다. 문자열을 미리 만들어 넘기면
/// 언어를 바꿔도 이전 언어의 문구가 그대로 남는다.
/// </summary>
public readonly struct ConfirmMessageLine
{
    public string LocKey { get; }

    /// <summary>문구의 {0}, {1}에 채울 값들. 서식이 없는 문구면 비어 있다.</summary>
    public object[] Args { get; }

    public ConfirmMessageLine(string locKey, params object[] args)
    {
        LocKey = locKey;
        Args = args;
    }
}
