using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

/// <summary>
/// 오늘 웨이브 데이터에 편성되고 해금된 포탈의 경로 라인만 보이도록 한다.
/// 포탈 하나가 여러 루트를 가질 수 있으므로, WaveRoutePlanner가 실제로 적을 배정한 루트만 켠다.
/// 스폰 활성화(PortalProgressionController.SetSpawnActive)와는 별개로, 순수 비주얼만 담당한다.
/// </summary>
public class PortalPathVisibility : MonoBehaviour
{
    [SerializeField] private WaveCycleProgression _waveCycleProgression;

    [Tooltip("점령 페널티로 추가된 적이 어느 루트로 가는지 계산하는 데 씁니다. WaveManager와 같은 참조여야 합니다.")]
    [SerializeField] private EnemyEnhancementManager _enemyEnhancementManager;

    [SerializeField] private List<Portal> _portals;
    [SerializeField] private Transform _pathRoot;

    private readonly Dictionary<PortalDirection, Portal> _portalById = new();
    private readonly List<(LineRenderer LineRenderer, PathFlowEffect FlowEffect)> _allPathLines = new();
    private readonly Dictionary<SplineContainer, LineRenderer> _lineByPath = new();
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

        // 점령으로 강화 프로필이 바뀌면 루트별 최종 적 수가 달라진다.
        // 편성에 없는 루트가 새로 열리지는 않지만, 최종 수가 0이 되거나 0에서 늘어나는 루트가 있을 수 있다.
        if (_enemyEnhancementManager != null)
        {
            _enemyEnhancementManager.ProfilesChanged += HandleProfilesChanged;
        }

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

        if (_enemyEnhancementManager != null)
        {
            _enemyEnhancementManager.ProfilesChanged -= HandleProfilesChanged;
        }
    }

    private void CachePathLines()
    {
        _allPathLines.Clear();
        _lineByPath.Clear();

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

            if (lineRenderer.TryGetComponent(out SplineContainer path))
            {
                _lineByPath[path] = lineRenderer;
            }
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

    private void HandleProfilesChanged(TerrainType terrainType)
    {
        if (!_waveCycleProgression.HasCurrentSnapshot)
        {
            return;
        }

        ApplyCurrentDay(_waveCycleProgression.CurrentSnapshot.TotalDay);
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

            if (!portal.IsActive)
            {
                continue;
            }

            CollectVisibleRoutes(portalWave, portal);
        }

        foreach ((LineRenderer lineRenderer, PathFlowEffect flowEffect) in _allPathLines)
        {
            SetLineVisible(lineRenderer, flowEffect, _visibleLinesToday.Contains(lineRenderer));
        }
    }

    private void CollectVisibleRoutes(PortalWaveData portalWave, Portal portal)
    {
        PortalRoutePlan portalPlan = WaveRoutePlanner.BuildPortalPlan(
            portalWave,
            portal,
            _enemyEnhancementManager);

        foreach (RouteSpawnPlan routePlan in portalPlan.Routes)
        {
            if (routePlan.Path == null)
            {
                continue;
            }

            if (_lineByPath.TryGetValue(routePlan.Path, out LineRenderer lineRenderer))
            {
                _visibleLinesToday.Add(lineRenderer);
            }
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
