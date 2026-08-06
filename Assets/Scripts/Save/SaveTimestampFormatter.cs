using System;
using System.Globalization;
using UnityEngine;

/// <summary>
/// 저장 시각(UTC)을 사용자에게 보여 줄 문자열로 바꾼다. 세이브 UI가 각자 ToString을 부르며
/// 문화권 처리를 흩뜨리지 않도록, 변환을 이 한 곳에 가둔다.
///
/// 세이브 파일에 UTC로 기록하고 표시 시점에 현지 시각으로 되돌리는 이유:
///  - 서머타임. .NET의 ToLocalTime은 "그 시점"의 DST 규칙을 적용하므로 자동으로 맞는다.
///    반대로 로컬 시각을 그대로 저장하면 가을 전환 구간(같은 시각이 두 번 오는 1시간)에서
///    어느 쪽인지 알 수 없어진다.
///  - SaveMetaDto.SavedAtUtc가 string이 아니라 DateTimeOffset인 것도 같은 맥락이다.
///    직접 ToString/Parse를 쓰는 순간 CurrentCulture 버그가 생기는데, Newtonsoft는
///    DateTimeOffset을 항상 InvariantCulture ISO-8601로 왕복시킨다. DateTime과 달리
///    오프셋이 텍스트에 실려 직렬화 설정과 무관하게 왕복이 완전하다.
/// </summary>
public static class SaveTimestampFormatter
{
    // 언어 코드(ko_kr)와 문화권 이름(ko-KR)의 구분자만 다르다.
    private const char LANGUAGE_CODE_SEPARATOR = '_';
    private const char CULTURE_NAME_SEPARATOR = '-';

    // 스트링테이블에 서식 키가 없을 때 쓰는 최후 수단. GetString은 키가 없으면 키 문자열을
    // 그대로 돌려주는데, 그걸 서식으로 넘기면 FormatException이 난다.
    private const string FALLBACK_TIMESTAMP_FORMAT = "yyyy-MM-dd HH:mm";

    private static string _cachedLanguage;
    private static CultureInfo _cachedCulture;

    /// <summary>
    /// 게임 언어에서 파생한 문화권. OS 로케일을 쓰지 않는 이유는 둘이 다를 수 있기 때문이다
    /// (en-US 윈도우에서 한국어로 플레이하는 경우).
    /// </summary>
    public static CultureInfo CurrentCulture
    {
        get
        {
            string language = StringTable.CurrentLanguage;

            if (_cachedCulture != null && _cachedLanguage == language)
            {
                return _cachedCulture;
            }

            _cachedLanguage = language;
            _cachedCulture = ResolveCulture(language);
            return _cachedCulture;
        }
    }

    /// <summary>"2026년 8월 4일 21:30" 처럼 저장 시각만 표시한다.</summary>
    public static string ToDisplayString(DateTimeOffset savedAtUtc)
    {
        if (savedAtUtc == default)
        {
            return StringTable.GetString(SaveLocKeys.SLOT_EMPTY);
        }

        string format = StringTable.GetString(SaveLocKeys.TIMESTAMP_FORMAT);

        // GetString은 등록되지 않은 키를 그대로 돌려준다 - 그 값을 서식으로 쓰면 예외가 난다.
        if (format == SaveLocKeys.TIMESTAMP_FORMAT)
        {
            format = FALLBACK_TIMESTAMP_FORMAT;
        }

        return savedAtUtc.ToLocalTime().ToString(format, CurrentCulture);
    }

    /// <summary>"마지막 저장: 2026년 8월 4일 21:30" 전체 라벨.</summary>
    public static string ToLastSavedLabel(DateTimeOffset savedAtUtc)
    {
        return string.Format(
            StringTable.GetString(SaveLocKeys.LAST_SAVED_LABEL),
            ToDisplayString(savedAtUtc));
    }

    private static CultureInfo ResolveCulture(string language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return CultureInfo.InvariantCulture;
        }

        try
        {
            return CultureInfo.GetCultureInfo(
                language.Replace(LANGUAGE_CODE_SEPARATOR, CULTURE_NAME_SEPARATOR));
        }
        catch (CultureNotFoundException exception)
        {
            Debug.LogWarning($"[SaveTimestampFormatter] 문화권을 찾지 못해 기본값을 씁니다: {exception.Message}");
            return CultureInfo.InvariantCulture;
        }
    }
}
