using UnityEditor;
using UnityEngine;

/// <summary>
/// 타워·새끼용 데이터의 효과음 필드를 설계 문서의 배선표대로 채우는 에디터 전용 도구.
/// 값의 출처는 Docs/타워_전투_효과음_설계.md §11이다.
///
/// 인스펙터로 17개 에셋을 하나씩 여는 대신 표를 코드로 남긴다 - 병합 사고나 에셋 되돌림 뒤에
/// 같은 상태를 다시 만들 수 있어야 한다. OptionalSoundId는 켜기 전까지 무음이므로
/// 배선을 빠뜨리면 조용히 사라진다. 표와 대조하기 쉬운 형태가 필요하다.
/// </summary>
public static class TowerSoundDataWiring
{
    private const string TOWER_ROOT = "Assets/Data/TowerData/";
    private const string BABY_DRAGON_ROOT = "Assets/Data/BabyDragon/";

    private const string LAUNCH_PROPERTY = "_launchSound";
    private const string RESOLVE_PROPERTY = "_resolveSound";
    private const string AURA_ON_PROPERTY = "_auraOnSound";

    private const string ENABLED_PROPERTY = "_isEnabled";
    private const string ID_PROPERTY = "_id";

    private readonly struct WiringSpec
    {
        public readonly string AssetPath;
        public readonly SoundId? Launch;
        public readonly SoundId? Resolve;
        public readonly SoundId? AuraOn;

        public WiringSpec(string root, string assetName, SoundId? launch, SoundId? resolve, SoundId? auraOn)
        {
            AssetPath = root + assetName + ".asset";
            Launch = launch;
            Resolve = resolve;
            AuraOn = auraOn;
        }
    }

    private static readonly WiringSpec[] SPECS =
    {
        // 투사체형 8종. TD_Arrow가 마나 타워다(파일명이 Arrow인 것은 오래된 이름이다).
        new(TOWER_ROOT, "TD_Arrow", SoundId.TowerManaLaunch, SoundId.TowerManaResolve, null),
        new(TOWER_ROOT, "TD_CrossBow", SoundId.TowerCrossbowLaunch, SoundId.TowerCrossbowResolve, null),
        new(TOWER_ROOT, "TD_Musket", SoundId.TowerMusketLaunch, SoundId.TowerMusketResolve, null),
        new(TOWER_ROOT, "TD_Anti_Air", SoundId.TowerAntiAirLaunch, SoundId.TowerAntiAirResolve, null),
        new(TOWER_ROOT, "TD_FireTower", SoundId.TowerFlameLaunch, SoundId.TowerFlameResolve, null),
        new(TOWER_ROOT, "TD_IceTower", SoundId.TowerFrostLaunch, SoundId.TowerFrostResolve, null),
        new(TOWER_ROOT, "TD_StoneTower", SoundId.TowerBoulderLaunch, SoundId.TowerBoulderResolve, null),
        new(TOWER_ROOT, "TD_LifeTower", SoundId.TowerLifeLaunch, SoundId.TowerLifeResolve, null),

        // 오라형 4종. 공용 ID 하나를 쓴다.
        new(TOWER_ROOT, "TD_TimeTower", null, null, SoundId.TowerAuraOn),
        new(TOWER_ROOT, "TD_StealthTower", null, null, SoundId.TowerAuraOn),
        new(TOWER_ROOT, "TD_SoulTower", null, null, SoundId.TowerAuraOn),
        new(TOWER_ROOT, "TD_EnhancementTower", null, null, SoundId.TowerAuraOn),

        // 가시 타워는 투사체도 오라도 없다. 명시적으로 전부 끈다.
        new(TOWER_ROOT, "TD_ThornTower", null, null, null),

        // 새끼용은 신규 클립을 받지 않는다. TowerAttack의 공용 발사음이 사라진 자리를 메우는 마이그레이션이다.
        new(BABY_DRAGON_ROOT + "Fire/", "BD_Fire", SoundId.TowerFire, null, null),
        new(BABY_DRAGON_ROOT + "Ice/", "BD_Ice", SoundId.TowerFire, null, null),
        new(BABY_DRAGON_ROOT + "Time/", "BD_Time", SoundId.TowerFire, null, null),
        new(BABY_DRAGON_ROOT + "Stone/", "BD_Stone", SoundId.TowerFire, null, null),
        new(BABY_DRAGON_ROOT + "Life/", "BD_Life", SoundId.TowerFire, null, null),
    };

    [MenuItem("TowerAndDragon/Sound/타워 효과음 데이터 배선")]
    private static void Wire()
    {
        int wired = 0;
        int missing = 0;

        foreach (WiringSpec spec in SPECS)
        {
            var data = AssetDatabase.LoadAssetAtPath<TowerData>(spec.AssetPath);
            if (data == null)
            {
                Debug.LogError($"데이터 에셋을 찾지 못했다: {spec.AssetPath}");
                missing++;
                continue;
            }

            var serialized = new SerializedObject(data);
            SetOptionalSound(serialized, LAUNCH_PROPERTY, spec.Launch);
            SetOptionalSound(serialized, RESOLVE_PROPERTY, spec.Resolve);
            SetOptionalSound(serialized, AURA_ON_PROPERTY, spec.AuraOn);
            serialized.ApplyModifiedProperties();

            EditorUtility.SetDirty(data);
            wired++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[TowerSoundDataWiring] 배선 {wired} · 에셋 누락 {missing}");
    }

    private static void SetOptionalSound(SerializedObject serialized, string propertyName, SoundId? id)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
        {
            Debug.LogError($"프로퍼티를 찾지 못했다: {propertyName} ({serialized.targetObject.name})");
            return;
        }

        property.FindPropertyRelative(ENABLED_PROPERTY).boolValue = id.HasValue;

        if (id.HasValue)
        {
            property.FindPropertyRelative(ID_PROPERTY).intValue = (int)id.Value;
        }
    }
}
