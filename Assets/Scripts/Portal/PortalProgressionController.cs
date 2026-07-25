using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 현재 주기에 맞는 포탈만 웨이브 스폰 대상으로 활성화한다.
/// </summary>
public class PortalProgressionController : MonoBehaviour
{
    [SerializeField] private WaveCycleProgression _waveCycleProgression;
    [SerializeField] private List<Portal> _portals;

    private void OnEnable()
    {
        if (_waveCycleProgression == null)
        {
            Debug.LogError(
                "[PortalProgressionController] WaveCycleProgression 참조가 없습니다.",
                this);
            return;
        }

        _waveCycleProgression.CycleStarted.AddListener(ApplyCycle);

        if (_waveCycleProgression.HasCurrentSnapshot)
        {
            ApplyCycle(_waveCycleProgression.CurrentSnapshot.CycleNumber);
        }
    }

    private void OnDisable()
    {
        if (_waveCycleProgression != null)
        {
            _waveCycleProgression.CycleStarted.RemoveListener(ApplyCycle);
        }
    }

    public void ApplyCycle(int cycleNumber)
    {
        if (!WaveCycleRules.IsValidCycleNumber(cycleNumber))
        {
            Debug.LogError(
                $"[PortalProgressionController] {cycleNumber}은 올바른 주기 번호가 아닙니다.",
                this);
            return;
        }

        HashSet<PortalDirection> activePortalDirections =
            new HashSet<PortalDirection>();

        for (int currentCycleNumber = WaveCycleRules.FIRST_CYCLE_NUMBER;
             currentCycleNumber <= cycleNumber;
             currentCycleNumber++)
        {
            if (WaveCycleRules.TryGetPortalToUnlock(
                    currentCycleNumber,
                    out PortalDirection portalDirection))
            {
                activePortalDirections.Add(portalDirection);
            }
        }

        if (_portals == null)
        {
            Debug.LogError("[PortalProgressionController] 포탈 목록이 없습니다.", this);
            return;
        }

        foreach (Portal portal in _portals)
        {
            if (portal == null)
            {
                Debug.LogError(
                    "[PortalProgressionController] 비어 있는 포탈 참조가 있습니다.",
                    this);
                continue;
            }

            portal.SetSpawnActive(
                activePortalDirections.Contains(portal.PortalDirectionId));
        }
    }

    private void OnValidate()
    {
        if (_portals == null)
        {
            return;
        }

        HashSet<PortalDirection> registeredPortalDirections =
            new HashSet<PortalDirection>();

        foreach (Portal portal in _portals)
        {
            if (portal == null)
            {
                Debug.LogError(
                    "[PortalProgressionController] 비어 있는 포탈 참조가 있습니다.",
                    this);
                continue;
            }

            if (!registeredPortalDirections.Add(portal.PortalDirectionId))
            {
                Debug.LogError(
                    $"[PortalProgressionController] {portal.PortalDirectionId} 포탈이 중복되었습니다.",
                    this);
            }
        }
    }
}
