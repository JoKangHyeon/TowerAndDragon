using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 한 포탈에서 실행할 시작 지연과 루트별 적 편성을 정의한다.
/// </summary>
[Serializable]
public class PortalWaveData
{
    [Tooltip("이 편성을 실행할 포탈의 식별자입니다.")]
    [SerializeField] private PortalDirection _portalDirectionId;

    [Tooltip("웨이브 시작 후 이 포탈의 편성을 시작하기까지 기다릴 시간(초)입니다.")]
    [SerializeField] private float _startDelay;

    [Tooltip("이 포탈에서 실행할 루트별 편성입니다. 루트마다 병렬로 진행됩니다.")]
    [SerializeField] private List<RouteWaveData> _routeWaves;

    public PortalDirection PortalDirectionId => _portalDirectionId;
    public float StartDelay => _startDelay;
    public IReadOnlyList<RouteWaveData> RouteWaves => _routeWaves;
}
