using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 오늘 웨이브 데이터에 편성되고 해금된 포탈의 경로 라인만 보이도록 한다.
/// 스폰 활성화(PortalProgressionController.SetSpawnActive)와는 별개로, 순수 비주얼만 담당한다.
/// </summary>
public class PortalPathVisibility : MonoBehaviour
{
    [SerializeField] private WaveCycleProgression _waveCycleProgression;
    [SerializeField] private List<Portal> _portals;
    [SerializeField] private Transform _pathRoot;

    private readonly Dictionary<PortalDirection, Portal> _portalById = new();
    private readonly List<(LineRenderer LineRenderer, PathFlowEffect FlowEffect)> _allPathLines = new();
    private readonly HashSet<LineRenderer> _visibleLinesToday = new();

    private void Awake()
    {
        CachePathLines();
        CachePortalMap();
    }

    private void OnEnable()
    {
        HideAllLines();

        if (_waveCycleProgression == null)
        {
            Debug.LogError("[PortalPathVisibility] WaveCycleProgression 참조가 없습니다.", this);
            return;
        }

        _waveCycleProgression.DayWaveResolved.AddListener(ApplyCurrentDay);

        if (_waveCycleProgression.HasCurrentSnapshot)
        {
            ApplyCurrentDay(_waveCycleProgression.CurrentSnapshot.TotalDay);
        }
    }

    private void OnDisable()
    {
        if (_waveCycleProgression != null)
        {
            _waveCycleProgression.DayWaveResolved.RemoveListener(ApplyCurrentDay);
        }
    }

    private void CachePathLines()
    {
        _allPathLines.Clear();

        if (_pathRoot == null)
        {
            Debug.LogError("[PortalPathVisibility] 경로 루트(_pathRoot) 참조가 없습니다.", this);
            return;
        }

        LineRenderer[] lineRenderers = _pathRoot.GetComponentsInChildren<LineRenderer>(true);

        foreach (LineRenderer lineRenderer in lineRenderers)
        {
            lineRenderer.TryGetComponent(out PathFlowEffect flowEffect);
            _allPathLines.Add((lineRenderer, flowEffect));
        }
    }

    private void CachePortalMap()
    {
        _portalById.Clear();

        if (_portals == null)
        {
            Debug.LogError("[PortalPathVisibility] 포탈 목록이 없습니다.", this);
            return;
        }

        foreach (Portal portal in _portals)
        {
            if (portal == null)
            {
                Debug.LogError("[PortalPathVisibility] 비어 있는 포탈 참조가 있습니다.", this);
                continue;
            }

            _portalById[portal.PortalDirectionId] = portal;
        }
    }

    private void ApplyCurrentDay(int totalDay)
    {
        if (!_waveCycleProgression.HasCurrentSnapshot)
        {
            HideAllLines();
            return;
        }

        WaveDefinitionSO waveDefinition = _waveCycleProgression.CurrentSnapshot.WaveDefinition;

        if (waveDefinition == null)
        {
            Debug.LogError("[PortalPathVisibility] 오늘 웨이브 데이터가 없습니다.", this);
            HideAllLines();
            return;
        }

        _visibleLinesToday.Clear();

        foreach (PortalWaveData portalWave in waveDefinition.PortalWaves)
        {
            if (portalWave == null)
            {
                continue;
            }

            if (!_portalById.TryGetValue(portalWave.PortalDirectionId, out Portal portal))
            {
                Debug.LogError(
                    $"[PortalPathVisibility] {portalWave.PortalDirectionId} 방향 포탈을 찾을 수 없습니다.",
                    this);
                continue;
            }

            if (!portal.IsActive || portal.GroundPath == null)
            {
                continue;
            }

            if (portal.GroundPath.TryGetComponent(out LineRenderer todayLine))
            {
                _visibleLinesToday.Add(todayLine);
            }
        }

        foreach ((LineRenderer lineRenderer, PathFlowEffect flowEffect) in _allPathLines)
        {
            SetLineVisible(lineRenderer, flowEffect, _visibleLinesToday.Contains(lineRenderer));
        }
    }

    private void HideAllLines()
    {
        foreach ((LineRenderer lineRenderer, PathFlowEffect flowEffect) in _allPathLines)
        {
            SetLineVisible(lineRenderer, flowEffect, false);
        }
    }

    private static void SetLineVisible(LineRenderer lineRenderer, PathFlowEffect flowEffect, bool isVisible)
    {
        lineRenderer.enabled = isVisible;

        if (flowEffect != null)
        {
            flowEffect.enabled = isVisible;
        }
    }
}
