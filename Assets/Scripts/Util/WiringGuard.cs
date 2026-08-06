using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

/// <summary>인스펙터 와이어링 누락·컴포넌트 조회 실패를 조용히 넘기지 않게 하는 가드.
/// 기존에 흩어져 있던 "if (_x == null) return;" 무음 early-out을 대체한다 - 참조가 비어 있으면
/// 에디터 콘솔에 원인이 드러나는 로그를 남기고, 호출부는 반환된 bool로 그대로 빠져나간다.
/// 로그 경로는 [Conditional("UNITY_EDITOR")]라 빌드에서는 호출 자체가 사라지고 널 검사만 남는다.</summary>
public static class WiringGuard
{
    private const string MISSING_REQUIRED_FORMAT = "[{0}] {1} 인스펙터 연결 필요 - 이 기능이 동작하지 않습니다.";
    private const string MISSING_OPTIONAL_FORMAT = "[{0}] {1} 인스펙터 미연결 - 관련 동작을 건너뜁니다.";
    private const string MISSING_COMPONENT_FORMAT = "[{0}] {1} 컴포넌트를 찾을 수 없습니다 - 프리팹 구성을 확인하세요.";
    private const string EMPTY_DATA_FORMAT = "[{0}] {1}가 비어 있습니다 - 데이터 에셋을 확인하세요.";
    private const string UNKNOWN_CONTEXT_NAME = "WiringGuard";

#if UNITY_EDITOR
    // (인스턴스ID, 멤버명)당 한 번만 보고한다 - 매 프레임 도는 가드가 콘솔을 채우는 것을 막는다.
    private static readonly HashSet<(int, string)> REPORTED_KEYS = new HashSet<(int, string)>();

    // 플레이모드 재진입 시 Reload Domain이 꺼져 있으면 이전 세션의 기록이 남아 경고가 전부 억제된다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        REPORTED_KEYS.Clear();
    }
#endif

    /// <summary>필수 참조. 비어 있으면 에디터에서 에러를 남기고 false를 반환한다.</summary>
    public static bool Require(Object reference, string memberName, Object context)
    {
        // UnityEngine.Object의 == 오버로드를 타야 파괴된 오브젝트도 널로 잡힌다.
        if (reference != null)
        {
            return true;
        }

        LogMissing(MISSING_REQUIRED_FORMAT, LogType.Error, memberName, context);
        return false;
    }

    /// <summary>없어도 되는 참조. 비어 있으면 에디터에서 경고만 남기고 false를 반환한다.</summary>
    public static bool Optional(Object reference, string memberName, Object context)
    {
        if (reference != null)
        {
            return true;
        }

        LogMissing(MISSING_OPTIONAL_FORMAT, LogType.Warning, memberName, context);
        return false;
    }

    /// <summary>UnityEngine.Object가 아닌 참조(순수 C# 객체 등)용 필수 검사.</summary>
    public static bool RequireRef<T>(T reference, string memberName, Object context) where T : class
    {
        // 제네릭 경로에서 reference == null은 Unity의 == 오버로드를 타지 않아
        // 파괴된 오브젝트를 "널 아님"으로 오판한다 - UnityEngine.Object면 명시적으로 되돌린다.
        bool isNull = reference is Object unityObject ? unityObject == null : reference == null;
        if (!isNull)
        {
            return true;
        }

        LogMissing(MISSING_REQUIRED_FORMAT, LogType.Error, memberName, context);
        return false;
    }

    /// <summary>데이터 에셋의 배열·리스트 필수 검사. 널이거나 비어 있으면 false를 반환한다.</summary>
    public static bool RequireNotEmpty<T>(IReadOnlyList<T> items, string memberName, Object context)
    {
        if (items != null && items.Count > 0)
        {
            return true;
        }

        LogMissing(EMPTY_DATA_FORMAT, LogType.Error, memberName, context);
        return false;
    }

    /// <summary>같은 게임오브젝트의 컴포넌트를 필수로 가져온다. 없으면 에디터에서 에러를 남기고 false를 반환한다.
    /// Awake의 무검사 GetComponent 캐싱과 순회 중의 TryGetComponent 가드 양쪽에 쓴다.</summary>
    public static bool RequireComponent<T>(Component owner, out T component, Object context = null)
        where T : Component
    {
        if (owner != null && owner.TryGetComponent(out component))
        {
            return true;
        }

        component = null;
        LogMissing(MISSING_COMPONENT_FORMAT, LogType.Error, typeof(T).Name, context != null ? context : owner);
        return false;
    }

    // 빌드에서는 이 호출 자체가 컴파일러에 의해 제거된다 - 공개 메서드에는 널 검사만 남는다.
    [Conditional("UNITY_EDITOR")]
    private static void LogMissing(string format, LogType logType, string memberName, Object context)
    {
#if UNITY_EDITOR
        // 캐시 조회는 실패 분기에서만 한다 - 매 프레임 도는 가드의 정상 경로에 비용을 주지 않기 위함.
        int contextId = context != null ? context.GetInstanceID() : 0;
        if (!REPORTED_KEYS.Add((contextId, memberName)))
        {
            return;
        }

        string contextName = context != null ? context.GetType().Name : UNKNOWN_CONTEXT_NAME;
        string message = string.Format(format, contextName, memberName);

        // 콘솔 항목을 클릭하면 해당 오브젝트가 하이라이트되도록 context를 함께 넘긴다.
        if (logType == LogType.Error)
        {
            Debug.LogError(message, context);
        }
        else
        {
            Debug.LogWarning(message, context);
        }
#endif
    }
}
