using System.Collections.Generic;
using System.Text;
using UnityEngine.InputSystem;

/// <summary>
/// 안내 문구의 <c>{0}</c>에 넣을 단축키 표기를 만든다(예: "B").
///
/// <see cref="InputActionRebindingExtensions.GetBindingDisplayString(InputAction, InputBinding.DisplayStringOptions, string)"/>
/// 를 쓰지 않는 이유: 그 함수는 바인딩에 매칭된 실제 KeyControl의 displayName을 쓰는데,
/// 그 값은 KeyControl.RefreshConfiguration이 OS에 QueryKeyNameCommand로 물어본 결과다.
/// 즉 "지금 켜져 있는 입력기 레이아웃의 글자"라서, 윈도우 한글(두벌식)이 켜져 있으면
/// <c>&lt;Keyboard&gt;/b</c>가 "ㅠ"로, <c>/c</c>가 "ㅊ"로, <c>/v</c>가 "ㅍ"로 나온다.
/// 언어 파일과는 무관하다 - 영문 빌드여도 입력기가 한글이면 똑같이 "ㅠ"가 나온다.
///
/// 대신 바인딩 경로를 레이아웃 정의(Keyboard의 <c>displayName = "B"</c>)로 풀어 입력기와
/// 무관하게 항상 같은 표기를 낸다. path가 아니라 effectivePath를 보므로 리바인딩은 그대로 따라간다.
///
/// 단일 키는 <see cref="Resolve(InputAction)"/>, WASD 같은 방향 합성 바인딩은
/// <see cref="ResolveDirectionKeys"/>를 쓴다.
///
/// 한계: <c>*/{Cancel}</c>처럼 용도(usage)로 적힌 경로는 실제 컨트롤을 봐야 풀리므로
/// 경로 그대로인 "Cancel"이 나온다. 지금 이 표기를 쓰는 곳(안내 문구·키 설정 목록)에는
/// 그런 액션이 없다 - 쓰게 되면 그 액션만 따로 다뤄야 한다.
/// </summary>
public static class InputBindingLabel
{
    // 한 액션에 키가 여럿 걸려 있을 때 이어 붙이는 구분자.
    private const string BINDING_SEPARATOR = " / ";

    // 방향 키 넷을 이어 붙이는 구분자. 한 덩어리로 읽혀야 하므로 공백을 두지 않는다.
    private const string DIRECTION_SEPARATOR = "/";

    // 방향 키를 늘어놓는 순서. 바인딩 파일의 순서(위·아래·왼쪽·오른쪽)를 그대로 쓰면
    // "W/S/A/D"가 되어 익숙한 배열과 어긋난다.
    private static readonly string[] DIRECTION_PART_ORDER = { "up", "left", "down", "right" };

    /// <summary>액션에 걸린 키들의 표기. 액션이 없거나 풀 수 있는 바인딩이 없으면 빈 문자열이다.</summary>
    public static string Resolve(InputAction action)
    {
        if (action == null)
        {
            return string.Empty;
        }

        StringBuilder builder = new StringBuilder();

        foreach (InputBinding binding in action.bindings)
        {
            if (binding.isComposite || binding.isPartOfComposite)
            {
                continue;
            }

            string text = ToPathBasedString(binding);

            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append(BINDING_SEPARATOR);
            }

            builder.Append(text);
        }

        return builder.ToString();
    }

    /// <summary>액션의 특정 바인딩 하나만의 표기. 범위를 벗어나면 빈 문자열이다.</summary>
    public static string Resolve(InputAction action, int bindingIndex)
    {
        if (action == null || bindingIndex < 0 || bindingIndex >= action.bindings.Count)
        {
            return string.Empty;
        }

        return ToPathBasedString(action.bindings[bindingIndex]);
    }

    /// <summary>
    /// 방향 합성 바인딩(2DVector·Dpad)의 키 표기. 예: "W/A/S/D".
    ///
    /// 한 방향에 키가 둘 이상 걸려 있으면(WASD와 방향키가 함께 걸린 지금이 그렇다) 첫 번째만 쓴다 -
    /// 여덟 개를 다 늘어놓으면 한 문장에 들어가지 않는다.
    /// 합성 바인딩이 없는 액션이면 빈 문자열이다.
    /// </summary>
    public static string ResolveDirectionKeys(InputAction action)
    {
        if (action == null)
        {
            return string.Empty;
        }

        Dictionary<string, string> labelsByPart = new Dictionary<string, string>();

        foreach (InputBinding binding in action.bindings)
        {
            if (!binding.isPartOfComposite || labelsByPart.ContainsKey(binding.name))
            {
                continue;
            }

            string text = ToPathBasedString(binding);

            if (!string.IsNullOrWhiteSpace(text))
            {
                labelsByPart[binding.name] = text;
            }
        }

        StringBuilder builder = new StringBuilder();

        foreach (string part in DIRECTION_PART_ORDER)
        {
            if (!labelsByPart.TryGetValue(part, out string text))
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append(DIRECTION_SEPARATOR);
            }

            builder.Append(text);
        }

        return builder.ToString();
    }

    // 매칭된 컨트롤을 넘기지 않는 것이 핵심이다 - 컨트롤을 넘기면 InputControlPath가
    // 그 컨트롤의 displayName(= OS가 준 입력기 글자)을 쓰고, 넘기지 않으면 경로를
    // 레이아웃 정의로 푼다.
    //
    // UseShortNames는 마우스·패드 표기를 기존 GetBindingDisplayString과 같게 유지한다
    // (없으면 "LMB"가 "Left Button", "X"가 "Button West"로 길어진다).
    // 키보드 키에는 짧은 이름이 없어 "B"·"Tab"·"Space" 그대로다.
    private static string ToPathBasedString(InputBinding binding) =>
        InputControlPath.ToHumanReadableString(
            binding.effectivePath,
            InputControlPath.HumanReadableStringOptions.OmitDevice
            | InputControlPath.HumanReadableStringOptions.UseShortNames);
}
