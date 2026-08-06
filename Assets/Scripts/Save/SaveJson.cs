using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

/// <summary>
/// 세이브 JSON 직렬화 설정의 단일 소유자. 저장 경로와 로드 경로가 반드시 같은 설정을 쓰도록 한다.
/// error 문자열은 전부 예외 메시지에서 나온다(CLAUDE.md 커밋규칙 §5).
/// </summary>
public static class SaveJson
{
    // JSON 프로퍼티 이름은 DTO 필드 이름과 그대로 일치시킨다(ContractResolver 미사용).
    // 버전 판정용으로 DTO 역직렬화 전에 직접 꺼내 읽는 경로가 있어, 이름 변환을 한 겹 끼우면
    // 그 경로와 DTO 정의가 어긋날 여지가 생긴다.
    private const string META_PROPERTY = "Meta";
    private const string SCHEMA_VERSION_PROPERTY = "SchemaVersion";

    public static JsonSerializerSettings Settings { get; } = new JsonSerializerSettings
    {
        // 수 KB 규모라 용량보다 사람이 열어 보는 가치가 크다.
        Formatting = Formatting.Indented,
        NullValueHandling = NullValueHandling.Ignore,

        // 구버전 세이브에 없는 필드는 DTO 기본값으로 남긴다(정규화가 뒤에서 걸러 준다).
        MissingMemberHandling = MissingMemberHandling.Ignore,

        // DateTimeOffset을 로컬 시각으로 조용히 바꾸지 않도록 명시한다.
        DateTimeZoneHandling = DateTimeZoneHandling.Utc,

        // 다형 역직렬화는 IL2CPP AOT 지뢰이자 보안 이슈다. 명시적으로 끈다.
        TypeNameHandling = TypeNameHandling.None,

        // DTO에 순환 참조가 생기면 조용히 무시하지 않고 즉시 드러낸다.
        ReferenceLoopHandling = ReferenceLoopHandling.Error,

        Culture = System.Globalization.CultureInfo.InvariantCulture,
    };

    public static bool TrySerialize(object value, out string json, out string error)
    {
        json = null;

        try
        {
            json = JsonConvert.SerializeObject(value, Settings);
            error = null;
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }

    public static bool TryDeserialize<T>(string json, out T value, out string error) where T : class
    {
        value = null;

        try
        {
            value = JsonConvert.DeserializeObject<T>(json, Settings);
            error = value == null ? "역직렬화 결과가 비어 있습니다." : null;
            return value != null;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }

    /// <summary>
    /// 로드는 항상 이 2단계 파싱을 거친다. DTO로 바로 역직렬화하면 필드 타입이 바뀐 구버전 세이브가
    /// 버전 판정 전에 예외로 터져 "왜 못 읽는지"를 진단할 수 없다.
    /// 나중에 마이그레이션을 넣는다면 이 JObject 단계에서 v -> v+1로 끌어올린 뒤 ToObject 한다.
    /// </summary>
    public static bool TryParseObject(string json, out JObject root, out string error)
    {
        root = null;

        try
        {
            root = JObject.Parse(json);
            error = null;
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }

    /// <summary>본문을 DTO로 만들기 전에 스키마 버전만 먼저 꺼낸다.</summary>
    public static bool TryReadSchemaVersion(JObject root, out int schemaVersion)
    {
        schemaVersion = 0;

        JToken versionToken = root?[META_PROPERTY]?[SCHEMA_VERSION_PROPERTY];
        if (versionToken == null)
        {
            return false;
        }

        try
        {
            schemaVersion = versionToken.Value<int>();
            return true;
        }
        catch (Exception exception)
        {
            UnityEngine.Debug.LogWarning($"[SaveJson] 스키마 버전을 읽지 못했습니다: {exception.Message}");
            return false;
        }
    }

    public static bool TryToObject<T>(JObject root, out T value, out string error) where T : class
    {
        value = null;

        try
        {
            value = root.ToObject<T>(JsonSerializer.Create(Settings));
            error = value == null ? "역직렬화 결과가 비어 있습니다." : null;
            return value != null;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }
}
