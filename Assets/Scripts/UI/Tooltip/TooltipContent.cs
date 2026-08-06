/// <summary>
/// 툴팁에 그대로 표시할 내용이다. 지역화·서식이 이미 끝난 문자열만 담는다
/// (표시기는 문자열을 해석하지 않으므로 자원·건물·연구 등 어떤 도메인이든 같은 표시기를 재사용할 수 있다).
/// Body에는 TMP 리치텍스트를 써도 된다.
/// </summary>
public readonly struct TooltipContent
{
    public string Title { get; }
    public string Body { get; }

    public bool HasContent =>
        !string.IsNullOrEmpty(Title) || !string.IsNullOrEmpty(Body);

    public TooltipContent(string title, string body)
    {
        Title = title;
        Body = body;
    }
}
