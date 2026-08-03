using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 한 포탈의 특정 루트(경로)로 내보낼 적 생성 그룹 목록을 정의한다.
/// 루트는 Portal.GroundPaths의 인덱스로 지정한다.
/// </summary>
[Serializable]
public class RouteWaveData
{
    [Tooltip("이 편성을 내보낼 경로입니다. Portal의 지상 경로 목록 인덱스입니다.")]
    [SerializeField] private int _routeIndex;

    [Tooltip("포탈의 시작 지연에 더해, 이 루트의 편성을 시작하기까지 기다릴 시간(초)입니다.")]
    [SerializeField] private float _startDelay;

    [Tooltip("이 루트에서 목록 순서대로 실행할 적 생성 그룹입니다.")]
    [SerializeField] private List<SpawnGroupData> _spawnGroups;

    public int RouteIndex => _routeIndex;
    public float StartDelay => _startDelay;
    public IReadOnlyList<SpawnGroupData> SpawnGroups => _spawnGroups;
}
