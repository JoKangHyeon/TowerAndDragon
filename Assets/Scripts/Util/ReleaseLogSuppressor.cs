using UnityEngine;

/// <summary>
/// 릴리스(비-Development) 플레이어 빌드에서 모든 Debug 로그 출력을 끈다.
///
/// 코드 전역에 흩어진 <see cref="Debug.Log"/> 호출을 일일이 제거하는 대신, Unity 로거 자체를
/// 시작 시점에 한 번 비활성화한다. Build Settings의 <b>Development Build</b> 체크가 이 동작을
/// 가른다 - 체크된 빌드와 에디터에서는 <see cref="Debug.isDebugBuild"/>가 true라 로그가 그대로 남고,
/// 체크되지 않은 제출용 릴리스 빌드에서만 로그가 사라진다.
/// </summary>
internal static class ReleaseLogSuppressor
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Apply()
    {
        if (Debug.isDebugBuild)
        {
            return;
        }

        Debug.unityLogger.logEnabled = false;
    }
}
