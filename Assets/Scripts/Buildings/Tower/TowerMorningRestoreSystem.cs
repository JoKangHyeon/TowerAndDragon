using UnityEngine;

/// <summary>
/// 아침(낮 시작)마다 모든 타워를 만피로 회복시키고, 부활 대기 중인 타워를 즉시 재활성화한다.
/// 복구 자체는 이미 있는 Tower.RestoreAtMorning()이 담당한다 - 새 복구 경로를 만들지 않는다.
/// CastleRegenSystem과 같은 독립 시스템 컴포넌트 스타일을 따른다 -
/// Tower에 CycleManager 참조를 새로 넣지 않는다.
/// </summary>
public sealed class TowerMorningRestoreSystem : MonoBehaviour
{
    [SerializeField] private CycleManager _cycleManager;
    [SerializeField] private GridMap _gridMap;

    private void OnEnable()
    {
        if (!WiringGuard.Require(_cycleManager, nameof(_cycleManager), this))
        {
            return;
        }

        _cycleManager.OnDayStart.AddListener(RestoreAll);
    }

    private void OnDisable()
    {
        if (_cycleManager == null)
        {
            return;
        }

        _cycleManager.OnDayStart.RemoveListener(RestoreAll);
    }

    // OnDayStart의 일차 인자는 쓰지 않는다 - 복구량은 날짜와 무관하다.
    private void RestoreAll(int _)
    {
        if (!WiringGuard.Require(_gridMap, nameof(_gridMap), this))
        {
            return;
        }

        foreach (Building building in _gridMap.Buildings)
        {
            if (building is Tower tower)
            {
                tower.RestoreAtMorning();
            }
        }
    }
}
