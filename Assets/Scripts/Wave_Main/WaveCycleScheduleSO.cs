using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    menuName = "TowerAndDragon/Wave/Wave Cycle Schedule",
    fileName = "WaveCycleSchedule")]
public class WaveCycleScheduleSO : ScriptableObject
{
    [SerializeField] private List<WaveCycleDefinition> _cycles;

    public IReadOnlyList<WaveCycleDefinition> Cycles => _cycles;
    public int WavesPerCycle => WaveCycleRules.WAVES_PER_CYCLE;

    public bool TryResolve(
        int totalDay,
        out WaveCycleSnapshot snapshot)
    {
        if (!WaveCycleRules.TryResolveDay(
                totalDay,
                out int cycleNumber,
                out int waveNumber))
        {
            snapshot = default;
            return false;
        }

        if (!TryGetCycleDefinition(cycleNumber, out WaveCycleDefinition cycleDefinition) ||
            !cycleDefinition.TryGetWaveDefinition(waveNumber, out WaveDefinitionSO waveDefinition))
        {
            snapshot = default;
            return false;
        }

        List<PortalDirection> activePortals = new List<PortalDirection>(cycleNumber);

        for (int currentCycleNumber = WaveCycleRules.FIRST_CYCLE_NUMBER;
             currentCycleNumber <= cycleNumber;
             currentCycleNumber++)
        {
            if (!TryGetCycleDefinition(
                    currentCycleNumber,
                    out WaveCycleDefinition activeCycleDefinition))
            {
                snapshot = default;
                return false;
            }

            activePortals.Add(activeCycleDefinition.PortalToUnlock);
        }

        snapshot = new WaveCycleSnapshot(
            totalDay,
            cycleNumber,
            waveNumber,
            waveDefinition,
            activePortals);

        return true;
    }

    private bool TryGetCycleDefinition(
        int cycleNumber,
        out WaveCycleDefinition cycleDefinition)
    {
        if (_cycles != null)
        {
            foreach (WaveCycleDefinition cycle in _cycles)
            {
                if (cycle != null && cycle.CycleNumber == cycleNumber)
                {
                    cycleDefinition = cycle;
                    return true;
                }
            }
        }

        cycleDefinition = null;
        return false;
    }

    private void OnValidate()
    {
        if (_cycles == null)
        {
            Debug.LogError("[WaveCycleScheduleSO] 주기 목록이 없습니다.", this);
            return;
        }

        if (_cycles.Count > WaveCycleRules.MAX_CYCLE_COUNT)
        {
            Debug.LogError(
                $"[WaveCycleScheduleSO] 주기는 최대 {WaveCycleRules.MAX_CYCLE_COUNT}개까지 등록할 수 있습니다.",
                this);
        }

        HashSet<int> registeredCycleNumbers = new HashSet<int>();
        HashSet<PortalDirection> registeredPortals = new HashSet<PortalDirection>();

        foreach (WaveCycleDefinition cycle in _cycles)
        {
            ValidateCycle(cycle, registeredCycleNumbers, registeredPortals);
        }

        for (int cycleNumber = WaveCycleRules.FIRST_CYCLE_NUMBER;
             cycleNumber <= _cycles.Count;
             cycleNumber++)
        {
            if (!registeredCycleNumbers.Contains(cycleNumber))
            {
                Debug.LogError(
                    $"[WaveCycleScheduleSO] {cycleNumber}주기 데이터가 순서대로 등록되지 않았습니다.",
                    this);
            }
        }
    }

    private void ValidateCycle(
        WaveCycleDefinition cycle,
        HashSet<int> registeredCycleNumbers,
        HashSet<PortalDirection> registeredPortals)
    {
        if (cycle == null)
        {
            Debug.LogError("[WaveCycleScheduleSO] 비어 있는 주기 데이터가 있습니다.", this);
            return;
        }

        if (!WaveCycleRules.IsValidCycleNumber(cycle.CycleNumber))
        {
            Debug.LogError(
                $"[WaveCycleScheduleSO] {cycle.CycleNumber}은 올바른 주기 번호가 아닙니다.",
                this);
        }
        else if (!registeredCycleNumbers.Add(cycle.CycleNumber))
        {
            Debug.LogError(
                $"[WaveCycleScheduleSO] {cycle.CycleNumber}주기 데이터가 중복되었습니다.",
                this);
        }

        ValidatePortal(cycle, registeredPortals);
        ValidateWaves(cycle);
    }

    private void ValidatePortal(
        WaveCycleDefinition cycle,
        HashSet<PortalDirection> registeredPortals)
    {
        if (cycle.PortalToUnlock == PortalDirection.None)
        {
            Debug.LogError(
                $"[WaveCycleScheduleSO] {cycle.CycleNumber}주기의 해금 포탈이 지정되지 않았습니다.",
                this);
            return;
        }

        if (!registeredPortals.Add(cycle.PortalToUnlock))
        {
            Debug.LogError(
                $"[WaveCycleScheduleSO] {cycle.PortalToUnlock} 포탈 해금이 중복되었습니다.",
                this);
        }

        if (WaveCycleRules.TryGetPortalToUnlock(
                cycle.CycleNumber,
                out PortalDirection expectedPortal) &&
            cycle.PortalToUnlock != expectedPortal)
        {
            Debug.LogError(
                $"[WaveCycleScheduleSO] {cycle.CycleNumber}주기에는 {expectedPortal} 포탈이 해금되어야 합니다.",
                this);
        }
    }

    private void ValidateWaves(WaveCycleDefinition cycle)
    {
        IReadOnlyList<WaveDefinitionSO> waves = cycle.Waves;

        if (waves == null)
        {
            Debug.LogError(
                $"[WaveCycleScheduleSO] {cycle.CycleNumber}주기의 웨이브 목록이 없습니다.",
                this);
            return;
        }

        if (waves.Count != WaveCycleRules.WAVES_PER_CYCLE)
        {
            Debug.LogError(
                $"[WaveCycleScheduleSO] {cycle.CycleNumber}주기에는 웨이브가 " +
                $"{WaveCycleRules.WAVES_PER_CYCLE}개 있어야 합니다.",
                this);
        }

        for (int waveIndex = 0; waveIndex < waves.Count; waveIndex++)
        {
            if (waves[waveIndex] == null)
            {
                Debug.LogError(
                    $"[WaveCycleScheduleSO] {cycle.CycleNumber}주기 {waveIndex + 1}번째 웨이브가 비어 있습니다.",
                    this);
            }
        }
    }
}
