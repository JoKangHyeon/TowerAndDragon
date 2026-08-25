using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>지형별 적 강화 프로필을 모아 두고, 웨이브 계획·미리보기가 읽어 가는 곳.
///
/// 출처가 둘이다. (1) 점령으로 지형별 프로필이 늘어나는 기존 경로(<see cref="ApplyProfile"/>)와
/// (2) 새 게임 +의 런 전역 프로필(<see cref="AddRunProfile"/>). 후자를 여기에 합류시키는 이유는
/// PortalWavePreviewRenderer가 WaveRoutePlanner를 거쳐 이 목록을 읽기 때문이다 -
/// 뮤테이터로 강화된 체력이 밤 전 미리보기 카드에 <b>자동으로</b> 표시된다.
/// 플레이어가 "오늘 뭐가 오는지"를 정확히 볼 수 있어야 제약이 불합리가 아니라 도전으로 읽힌다.</summary>
public class EnemyEnhancementManager : MonoBehaviour
{
    private readonly Dictionary<
        TerrainType,
        List<EnemyEnhancementProfileSO>> _profilesByTerrain = new();

    // 런 전역 프로필(새 게임 + 뮤테이터). 지형과 무관하게 모든 지형 목록에 합쳐진다.
    private readonly List<EnemyEnhancementProfileSO> _runProfiles = new();

    // (런 프로필 + 지형 프로필) 병합 결과. GetProfiles가 호출마다 리스트를 새로 만들지 않게 한다 -
    // PortalWavePreviewRenderer.Refresh가 포탈마다 이 메서드를 부른다.
    private readonly Dictionary<
        TerrainType,
        List<EnemyEnhancementProfileSO>> _mergedByTerrain = new();

    private bool _hasPulledRunProfiles;

    [Tooltip("새 게임 +(뮤테이터) 서비스. 비워 두면 런 전역 프로필이 없는 표준 모드로 동작한다"
        + " - 튜토리얼·테스트 씬에는 두지 않는다.")]
    [SerializeField]
    [WiringOptional]
    private RunModifierService _runModifierService;

    public event Action<TerrainType> ProfilesChanged;

    public void ApplyProfile(
        TerrainType terrainType,
        EnemyEnhancementProfileSO profile)
    {
        if (profile == null)
        {
            return;
        }

        if (!_profilesByTerrain.TryGetValue(
            terrainType,
            out List<EnemyEnhancementProfileSO> profiles))
        {
            profiles = new List<EnemyEnhancementProfileSO>();
            _profilesByTerrain.Add(terrainType, profiles);
        }

        profiles.Add(profile);
        _mergedByTerrain.Remove(terrainType);
        Debug.Log(
            $"[EnemyEnhancementManager] Terrain={terrainType}, Profile={profile.name}, " +
            $"AppliedProfileCount={profiles.Count}",
            this);
        ProfilesChanged?.Invoke(terrainType);
    }

    /// <summary>런 전역 강화 프로필을 추가한다(새 게임 + 뮤테이터).
    ///
    /// <b>ProfilesChanged를 발화하지 않는다.</b> 그 이벤트는 런 도중 점령으로 프로필이 늘어날 때
    /// 이미 그려진 화면을 다시 그리려고 있는 것이다. 런 시작 프로필은 아직 아무것도 그려지지 않은
    /// 시점이라 통지할 대상이 없고, 발화하면 CLAUDE.md의 "첫 발화는 Start 이후" 규칙과도 부딪힌다.</summary>
    public void AddRunProfile(EnemyEnhancementProfileSO profile)
    {
        if (profile == null)
        {
            return;
        }

        _runProfiles.Add(profile);
        _mergedByTerrain.Clear();
    }

    public IReadOnlyList<EnemyEnhancementProfileSO> GetProfiles(
        TerrainType terrainType)
    {
        EnsureRunProfilesPulled();

        // 런 프로필이 없으면 뮤테이터 이전과 완전히 같은 경로다 - 병합도 할당도 하지 않는다.
        if (_runProfiles.Count == 0)
        {
            if (_profilesByTerrain.TryGetValue(
                terrainType,
                out List<EnemyEnhancementProfileSO> plainProfiles))
            {
                return plainProfiles;
            }

            return Array.Empty<EnemyEnhancementProfileSO>();
        }

        if (_mergedByTerrain.TryGetValue(
            terrainType,
            out List<EnemyEnhancementProfileSO> merged))
        {
            return merged;
        }

        merged = new List<EnemyEnhancementProfileSO>(_runProfiles);

        if (_profilesByTerrain.TryGetValue(
            terrainType,
            out List<EnemyEnhancementProfileSO> terrainProfiles))
        {
            merged.AddRange(terrainProfiles);
        }

        _mergedByTerrain.Add(terrainType, merged);
        return merged;
    }

    // 서비스에서 pull하는 이유(push가 아니라): 세이브 복원 경로는 RunModifierService.Awake가 아니라
    // 그 뒤의 ApplyRestoredMutators로 뮤테이터를 확정한다. Awake에서 push받으면 이어하기한 런은
    // 적 강화 프로필이 영영 비어 있게 된다. 첫 GetProfiles는 밤 첫 계획·미리보기 갱신 시점이라
    // Awake 순서와 세이브 복원 양쪽보다 확실히 늦다.
    private void EnsureRunProfilesPulled()
    {
        if (_hasPulledRunProfiles)
        {
            return;
        }

        _hasPulledRunProfiles = true;

        if (_runModifierService == null)
        {
            return;
        }

        int before = _runProfiles.Count;
        _runModifierService.CollectEnemyProfiles(_runProfiles);

        if (_runProfiles.Count != before)
        {
            _mergedByTerrain.Clear();
        }
    }
}
