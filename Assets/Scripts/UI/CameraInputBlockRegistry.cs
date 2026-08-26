using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 카메라 조작을 막아야 하는 UI가 자기를 올려 두는 곳. 하나라도 올라와 있으면
/// <see cref="CameraController"/>가 그 프레임의 입력을 통째로 접는다.
///
/// static인 이유는 <see cref="GuideAnchorRegistry"/>와 같다 - 창은 전부 프리팹에 들어 있고
/// 카메라는 씬마다 따로 있는 오브젝트라, 참조로 이으면 씬 수만큼 배선이 늘고
/// 씬 하나를 되돌릴 때마다 조용히 끊긴다.
///
/// 목록으로 세는 이유는 <see cref="GameSpeedManager"/>의 창 일시정지 카운터가 개수로 세는 것과
/// 같다 - 창은 겹쳐 열린다(설정 창 → 슬롯 확인 팝업). 개수(int) 대신 대상을 들고 있는 이유는
/// 짝이 어긋났을 때 스스로 회복하기 위해서다 - 개수가 어긋나면 카메라가 영영 죽는다.
/// </summary>
public static class CameraInputBlockRegistry
{
    private static readonly List<Component> _blockers = new();

    /// <summary>지금 카메라 조작을 막아야 하는가.</summary>
    public static bool IsBlocked
    {
        get
        {
            PruneDestroyed();
            return _blockers.Count > 0;
        }
    }

    /// <summary>같은 대상을 두 번 넣어도 한 번만 등록된다(UIManager.AddOpenQuery와 같은 관례).</summary>
    public static void Register(Component blocker)
    {
        if (blocker != null && !_blockers.Contains(blocker))
        {
            _blockers.Add(blocker);
        }
    }

    /// <summary>자기가 넣은 것만 뺀다.</summary>
    public static void Unregister(Component blocker)
    {
        _blockers.Remove(blocker);
    }

    // OnDisable을 타지 못하고 파괴된 항목을 걸러낸다(씬 전환 등).
    // GuideAnchorRegistry.TryGet이 같은 방어를 한다 - 튜토리얼 → 본게임 경로가 실제로 그렇다.
    private static void PruneDestroyed()
    {
        for (int i = _blockers.Count - 1; i >= 0; i--)
        {
            if (_blockers[i] == null)
            {
                _blockers.RemoveAt(i);
            }
        }
    }

    // 이 프로젝트는 도메인 리로드를 켠 채로 쓰므로 정적 상태는 플레이 진입마다 초기화된다.
    // 그래도 여기만 명시적으로 지우는 이유: 남은 항목 하나가 곧 "카메라가 영영 안 움직임"이라
    // 나중에 누가 도메인 리로드를 끄는 순간 원인을 못 찾는 버그가 된다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        _blockers.Clear();
    }
}
