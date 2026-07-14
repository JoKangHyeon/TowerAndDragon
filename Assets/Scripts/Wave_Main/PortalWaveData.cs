using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 한 포탈에서 실행할 시작 지연과 적 생성 그룹 목록을 정의한다.
/// </summary>
[Serializable]
public class PortalWaveData
{
    [Tooltip("이 편성을 실행할 포탈의 식별자입니다.")]
    [SerializeField] private PortalId _portalId;

    [Tooltip("웨이브 시작 후 이 포탈의 편성을 시작하기까지 기다릴 시간(초)입니다.")]
    [SerializeField] private float _startDelay;

    [Tooltip("이 포탈에서 목록 순서대로 실행할 적 생성 그룹입니다.")]
    [SerializeField] private List<SpawnGroupData> _spawnGroups = new();

    public PortalId PortalId => _portalId;
    public float StartDelay => _startDelay;
    public IReadOnlyList<SpawnGroupData> SpawnGroups => _spawnGroups;
}
