using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// 런 전역 프로필(enemy_ferocity / enemy_tide)이 지형별 프로필과 병합돼 나오는지,
// 그리고 런 프로필이 없을 때 뮤테이터 이전과 결과가 같은지를 본다.
// 후자가 회귀 방어선이다 - GetProfiles는 WaveRoutePlanner와 포탈 미리보기가 함께 쓰는 경로다.
public class EnemyEnhancementManagerRunProfileTests
{
    private readonly List<Object> _created = new();

    [TearDown]
    public void TearDown()
    {
        foreach (Object asset in _created)
        {
            if (asset != null)
            {
                Object.DestroyImmediate(asset);
            }
        }

        _created.Clear();
    }

    [Test]
    public void GetProfiles_NoRunProfile_ReturnsOnlyTerrainProfiles()
    {
        EnemyEnhancementManager manager = MakeManager();
        EnemyEnhancementProfileSO terrainProfile = MakeProfile();

        manager.ApplyProfile(TerrainType.Snow, terrainProfile);

        Assert.That(manager.GetProfiles(TerrainType.Snow), Is.EqualTo(new[] { terrainProfile }));
    }

    [Test]
    public void GetProfiles_NoProfilesAtAll_ReturnsEmptyForEveryTerrain()
    {
        EnemyEnhancementManager manager = MakeManager();

        Assert.That(manager.GetProfiles(TerrainType.Snow), Is.Empty);
        Assert.That(manager.GetProfiles(TerrainType.Rock), Is.Empty);
    }

    [Test]
    public void GetProfiles_RunProfile_AppliesToEveryTerrainEvenWithoutTerrainProfiles()
    {
        EnemyEnhancementManager manager = MakeManager();
        EnemyEnhancementProfileSO runProfile = MakeProfile();

        manager.AddRunProfile(runProfile);

        Assert.That(manager.GetProfiles(TerrainType.Snow), Is.EqualTo(new[] { runProfile }));
        Assert.That(manager.GetProfiles(TerrainType.Rock), Is.EqualTo(new[] { runProfile }));
    }

    [Test]
    public void GetProfiles_RunAndTerrainProfiles_AreMerged()
    {
        EnemyEnhancementManager manager = MakeManager();
        EnemyEnhancementProfileSO runProfile = MakeProfile();
        EnemyEnhancementProfileSO terrainProfile = MakeProfile();

        manager.AddRunProfile(runProfile);
        manager.ApplyProfile(TerrainType.Snow, terrainProfile);

        Assert.That(
            manager.GetProfiles(TerrainType.Snow),
            Is.EqualTo(new[] { runProfile, terrainProfile }));

        // 다른 지형에는 지형 프로필이 새지 않는다.
        Assert.That(manager.GetProfiles(TerrainType.Rock), Is.EqualTo(new[] { runProfile }));
    }

    // 캐시를 넣었으므로, 점령으로 지형 프로필이 늘어난 뒤에도 새 값이 보여야 한다.
    [Test]
    public void ApplyProfile_AfterCachedRead_InvalidatesTheMergedCache()
    {
        EnemyEnhancementManager manager = MakeManager();
        EnemyEnhancementProfileSO runProfile = MakeProfile();
        EnemyEnhancementProfileSO firstTerrain = MakeProfile();
        EnemyEnhancementProfileSO secondTerrain = MakeProfile();

        manager.AddRunProfile(runProfile);
        manager.ApplyProfile(TerrainType.Snow, firstTerrain);

        // 여기서 캐시가 만들어진다.
        Assert.That(manager.GetProfiles(TerrainType.Snow).Count, Is.EqualTo(2));

        manager.ApplyProfile(TerrainType.Snow, secondTerrain);

        Assert.That(
            manager.GetProfiles(TerrainType.Snow),
            Is.EqualTo(new[] { runProfile, firstTerrain, secondTerrain }));
    }

    [Test]
    public void AddRunProfile_AfterCachedRead_InvalidatesEveryTerrain()
    {
        EnemyEnhancementManager manager = MakeManager();
        EnemyEnhancementProfileSO firstRun = MakeProfile();
        EnemyEnhancementProfileSO secondRun = MakeProfile();

        manager.AddRunProfile(firstRun);
        Assert.That(manager.GetProfiles(TerrainType.Snow).Count, Is.EqualTo(1));

        manager.AddRunProfile(secondRun);

        Assert.That(manager.GetProfiles(TerrainType.Snow), Is.EqualTo(new[] { firstRun, secondRun }));
    }

    [Test]
    public void AddRunProfile_Null_IsIgnored()
    {
        EnemyEnhancementManager manager = MakeManager();

        manager.AddRunProfile(null);

        Assert.That(manager.GetProfiles(TerrainType.Snow), Is.Empty);
    }

    private EnemyEnhancementManager MakeManager()
    {
        GameObject host = new GameObject(nameof(EnemyEnhancementManager));
        _created.Add(host);
        return host.AddComponent<EnemyEnhancementManager>();
    }

    private EnemyEnhancementProfileSO MakeProfile()
    {
        EnemyEnhancementProfileSO profile = ScriptableObject.CreateInstance<EnemyEnhancementProfileSO>();
        _created.Add(profile);
        return profile;
    }
}
