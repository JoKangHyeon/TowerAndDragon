using UnityEditor;
using UnityEngine;

/// <summary>
/// 타워 전투 효과음 17개를 SoundCatalog에 채워 넣는 에디터 전용 도구.
/// 값의 출처는 Docs/타워_전투_효과음_설계.md §7-1(클립)과 §8(믹싱)이다.
///
/// 손으로 17개를 넣으면 id·클립을 잘못 짝지어도 알아채기 어렵고, .asset YAML을 직접 고치면
/// Unity가 재직렬화하며 덮어쓴다. 그래서 에디터 API로만 쓴다.
/// 기존 0~13 항목은 읽지도 쓰지도 않는다. 이미 있는 신규 항목은 값만 갱신한다.
/// </summary>
public static class TowerSoundCatalogSetup
{
    private const string CATALOG_PATH = "Assets/Data/SoundCatalog.asset";
    private const string CLIP_ROOT = "Assets/Imported/Gamemaster Audio - Pro Sound Collection/";

    private const string SOUNDS_PROPERTY = "_sounds";
    private const string ID_PROPERTY = "_id";
    private const string CLIP_PROPERTY = "_clip";
    private const string VOLUME_PROPERTY = "_volume";
    private const string PITCH_RANGE_PROPERTY = "_pitchRange";
    private const string MIN_INTERVAL_PROPERTY = "_minInterval";

    private const string TOWER_VOLUME_SCALE_PROPERTY = "_seTowerVolumeScale";
    private const string FULL_VOLUME_RATIO_PROPERTY = "_seFullVolumeViewportRatio";
    private const string CUTOFF_RATIO_PROPERTY = "_seCutoffViewportRatio";
    private const string PAN_AMOUNT_PROPERTY = "_sePanAmount";
    private const string ZOOM_REFERENCE_SIZE_PROPERTY = "_seZoomReferenceSize";
    private const string MIN_ZOOM_VOLUME_PROPERTY = "_seMinZoomVolumeScale";

    private const float TOWER_VOLUME_SCALE = 1f;
    private const float FULL_VOLUME_RATIO = 1f;
    private const float CUTOFF_RATIO = 1.8f;
    private const float PAN_AMOUNT = 0.6f;
    private const float ZOOM_REFERENCE_SIZE = 6f;
    private const float MIN_ZOOM_VOLUME_SCALE = 0.4f;

    private readonly struct EntrySpec
    {
        public readonly SoundId Id;
        public readonly string ClipPath;
        public readonly float Volume;
        public readonly Vector2 PitchRange;
        public readonly float MinInterval;

        public EntrySpec(SoundId id, string clipPath, float volume, float pitchMin, float pitchMax, float minInterval)
        {
            Id = id;
            ClipPath = CLIP_ROOT + clipPath;
            Volume = volume;
            PitchRange = new Vector2(pitchMin, pitchMax);
            MinInterval = minInterval;
        }
    }

    private static readonly EntrySpec[] SPECS =
    {
        new(SoundId.TowerManaLaunch, "Magic_Spells/casting_charge_matter_fast_01.wav", 0.35f, 0.94f, 1.06f, 0.20f),
        new(SoundId.TowerManaResolve, "Magic_Spells/energy_blast_small_02.wav", 0.42f, 0.94f, 1.06f, 0.20f),
        new(SoundId.TowerCrossbowLaunch, "Guns_Weapons/Bow_Arrow/bow_crossbow_arrow_shoot_type1_02.wav", 0.40f, 0.94f, 1.06f, 0.20f),
        new(SoundId.TowerCrossbowResolve, "Punches/kick_soft_jab_impact_01.wav", 0.45f, 0.94f, 1.06f, 0.20f),
        new(SoundId.TowerMusketLaunch, "Guns_Weapons/Bullets/bullet_flyby_01.wav", 0.45f, 0.96f, 1.04f, 0.30f),
        new(SoundId.TowerMusketResolve, "Guns_Weapons/Guns/weapon_cannon_shot_01.wav", 0.48f, 0.96f, 1.04f, 0.30f),
        new(SoundId.TowerAntiAirLaunch, "Magic_Spells/casting_charge_matter_fast_01.wav", 0.38f, 0.80f, 0.88f, 0.30f),
        new(SoundId.TowerAntiAirResolve, "Magic_Spells/energy_blast_small_02.wav", 0.46f, 0.80f, 0.88f, 0.30f),
        new(SoundId.TowerFlameLaunch, "Magic_Spells/fireball_blast_projectile_spell_01.wav", 0.40f, 0.94f, 1.06f, 0.20f),
        new(SoundId.TowerFlameResolve, "Magic_Spells/fireball_impact_burn_01.wav", 0.55f, 0.94f, 1.06f, 0.20f),
        new(SoundId.TowerFrostLaunch, "Magic_Spells/ice_spell_forming_shards_04.wav", 0.40f, 0.94f, 1.06f, 0.20f),
        new(SoundId.TowerFrostResolve, "Magic_Spells/ice_blast_projectile_spell_03.wav", 0.55f, 0.94f, 1.06f, 0.20f),
        new(SoundId.TowerBoulderLaunch, "Magic_Spells/whoosh_magic_spell_02.wav", 0.45f, 0.96f, 1.04f, 0.35f),
        new(SoundId.TowerBoulderResolve, "Impacts_Smashable/rock_impact_small_hit_01.wav", 0.70f, 0.96f, 1.04f, 0.35f),
        new(SoundId.TowerLifeLaunch, "Magic_Spells/casting_charge_matter_fast_01.wav", 0.32f, 1.10f, 1.16f, 0.25f),
        new(SoundId.TowerLifeResolve, "Magic_Spells/healing_magic_spell_02.wav", 0.42f, 0.97f, 1.03f, 0.25f),
        new(SoundId.TowerAuraOn, "Magic_Spells/light_in_dark_spell_01.wav", 0.45f, 0.98f, 1.02f, 0.50f),

        // 새끼용 공격 모드. 클립을 타워와 공유하는 항목은 피치 대역을 겹치지 않게 잡는다(§7-2).
        new(SoundId.BabyDragonFireLaunch, "Magic_Spells/fireball_blast_projectile_spell_06.wav", 0.40f, 0.94f, 1.06f, 0.20f),
        new(SoundId.BabyDragonFireResolve, "Magic_Spells/fireball_impact_burn_01.wav", 0.45f, 1.10f, 1.16f, 0.20f),
        new(SoundId.BabyDragonIceLaunch, "Magic_Spells/ice_spell_forming_shards_04.wav", 0.40f, 1.08f, 1.14f, 0.20f),
        new(SoundId.BabyDragonIceResolve, "Magic_Spells/ice_spell_freeze_frost_01.wav", 0.50f, 0.94f, 1.06f, 0.20f),
        new(SoundId.BabyDragonTimeLaunch, "Magic_Spells/energy_blast_large_01.wav", 0.38f, 0.94f, 1.06f, 0.25f),
        new(SoundId.BabyDragonTimeResolve, "Magic_Spells/electric_surge_blast_02.wav", 0.45f, 0.94f, 1.06f, 0.25f),
        new(SoundId.BabyDragonStoneLaunch, "Magic_Spells/whoosh_magic_spell_01.wav", 0.45f, 0.96f, 1.04f, 0.35f),
        new(SoundId.BabyDragonStoneResolve, "Impacts_Smashable/rock_impact_heavy_slam_02.wav", 0.65f, 0.96f, 1.04f, 0.35f),
        new(SoundId.BabyDragonLifeLaunch, "Magic_Spells/casting_charge_matter_fast_01.wav", 0.32f, 1.20f, 1.26f, 0.25f),
        new(SoundId.BabyDragonLifeResolve, "Magic_Spells/nature_spell_bush_tree_whip_03.wav", 0.45f, 0.97f, 1.03f, 0.25f),
    };

    [MenuItem("TowerAndDragon/Sound/타워 효과음 카탈로그 채우기")]
    private static void Populate()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<SoundCatalog>(CATALOG_PATH);
        if (catalog == null)
        {
            Debug.LogError($"SoundCatalog을 찾지 못했다: {CATALOG_PATH}");
            return;
        }

        var serialized = new SerializedObject(catalog);
        SerializedProperty sounds = serialized.FindProperty(SOUNDS_PROPERTY);

        int added = 0;
        int updated = 0;
        int missingClips = 0;

        foreach (EntrySpec spec in SPECS)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(spec.ClipPath);
            if (clip == null)
            {
                Debug.LogError($"클립을 찾지 못했다: {spec.ClipPath}");
                missingClips++;
                continue;
            }

            SerializedProperty entry = FindEntry(sounds, spec.Id);
            if (entry == null)
            {
                sounds.arraySize++;
                entry = sounds.GetArrayElementAtIndex(sounds.arraySize - 1);
                added++;
            }
            else
            {
                updated++;
            }

            entry.FindPropertyRelative(ID_PROPERTY).intValue = (int)spec.Id;
            entry.FindPropertyRelative(CLIP_PROPERTY).objectReferenceValue = clip;
            entry.FindPropertyRelative(VOLUME_PROPERTY).floatValue = spec.Volume;
            entry.FindPropertyRelative(PITCH_RANGE_PROPERTY).vector2Value = spec.PitchRange;
            entry.FindPropertyRelative(MIN_INTERVAL_PROPERTY).floatValue = spec.MinInterval;
        }

        SetFloat(serialized, TOWER_VOLUME_SCALE_PROPERTY, TOWER_VOLUME_SCALE);
        SetFloat(serialized, FULL_VOLUME_RATIO_PROPERTY, FULL_VOLUME_RATIO);
        SetFloat(serialized, CUTOFF_RATIO_PROPERTY, CUTOFF_RATIO);
        SetFloat(serialized, PAN_AMOUNT_PROPERTY, PAN_AMOUNT);
        SetFloat(serialized, ZOOM_REFERENCE_SIZE_PROPERTY, ZOOM_REFERENCE_SIZE);
        SetFloat(serialized, MIN_ZOOM_VOLUME_PROPERTY, MIN_ZOOM_VOLUME_SCALE);

        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();

        // 파라미터도 함께 찍는다 - 인스펙터에서 조정한 값을 이 도구가 되돌렸을 때 콘솔만 봐도 알아채도록.
        Debug.Log(
            $"[TowerSoundCatalogSetup] 추가 {added} · 갱신 {updated} · 클립 누락 {missingClips}. " +
            $"파라미터: 마스터 {TOWER_VOLUME_SCALE} · 감쇠 {FULL_VOLUME_RATIO}/{CUTOFF_RATIO} · " +
            $"정위 {PAN_AMOUNT} · 줌 {ZOOM_REFERENCE_SIZE}/{MIN_ZOOM_VOLUME_SCALE}",
            catalog);
    }

    private static SerializedProperty FindEntry(SerializedProperty sounds, SoundId id)
    {
        for (int i = 0; i < sounds.arraySize; i++)
        {
            SerializedProperty entry = sounds.GetArrayElementAtIndex(i);
            if (entry.FindPropertyRelative(ID_PROPERTY).intValue == (int)id)
            {
                return entry;
            }
        }

        return null;
    }

    private static void SetFloat(SerializedObject serialized, string propertyName, float value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
        {
            Debug.LogError($"프로퍼티를 찾지 못했다: {propertyName}");
            return;
        }

        property.floatValue = value;
    }
}
