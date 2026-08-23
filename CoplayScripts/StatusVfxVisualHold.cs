using UnityEditor;
using UnityEngine;

/// <summary>
/// 눈으로 보기 위한 하네스. 몬스터 세 마리를 크기 순으로 세우고 화상을 무한으로 걸어 둔다.
///
/// 확인하려는 것은 수치가 아니라 그림이다.
/// - 루트 스케일이 0.1 ~ 1.5로 갈리는 몬스터들에게 불이 같은 크기로 뜨는가
/// - 불이 몸통에 붙는가(발밑이나 머리 위로 새지 않는가)
/// - 전투를 가릴 만큼 크지 않은가
///
/// 지속시간을 무한으로 만든 임시 상태를 쓴다 - 3초짜리로는 스크린샷을 찍기 전에 꺼진다.
/// 애셋은 건드리지 않는다(메모리에만 만든다).
/// </summary>
public static class StatusVfxVisualHold
{
    private const string VFX_PATH =
        "Assets/Imported/Prefabs/Effects/Status/FX_Status_FireBurn.prefab";
    private const string PREFAB_FOLDER = "Assets/Data/MonsterData/MonsterPrefab";

    private const string HOLD_STATUS_ID = "probe_visual_hold";
    private const float SPACING = 2.2f;

    // 크기 사다리의 양 끝과 가운데. 셋이 같은 크기의 불을 달고 있어야 한다.
    private static readonly string[] TARGET_PREFABS =
    {
        "MP_Ground_ElementImmune_Stone", // 0.1
        "MP_Ground_Tank",                // 1.0
        "MP_Boss_2",                     // 1.5
    };

    public static void Execute()
    {
        if (!Application.isPlaying)
        {
            Debug.LogError("[StatusVfxVisualHold] 플레이 모드에서 실행해야 합니다.");
            return;
        }

        var vfx = AssetDatabase.LoadAssetAtPath<GameObject>(VFX_PATH);

        if (vfx == null)
        {
            Debug.LogError($"[StatusVfxVisualHold] 연출 프리팹이 없습니다: {VFX_PATH}");
            return;
        }

        // 지속시간 0 = 무한. 애셋이 아니라 메모리 인스턴스라 프로젝트에 남지 않는다.
        var hold = ScriptableObject.CreateInstance<DamageOverTimeStatusSO>();
        var serialized = new SerializedObject(hold);

        serialized.FindProperty("_statusId").stringValue = HOLD_STATUS_ID;
        serialized.FindProperty("_durationSeconds").floatValue = 0f;
        serialized.FindProperty("_damagePerTick").floatValue = 0f;
        serialized.FindProperty("_tickIntervalSeconds").floatValue = 1f;
        serialized.FindProperty("_activeVfxPrefab").objectReferenceValue = vfx;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        float x = 0f;

        foreach (string prefabName in TARGET_PREFABS)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"{PREFAB_FOLDER}/{prefabName}.prefab");

            if (prefab == null)
            {
                Debug.LogWarning($"[StatusVfxVisualHold] 건너뜀: {prefabName}");
                continue;
            }

            GameObject spawned = Object.Instantiate(
                prefab, new Vector3(x, 0f, 0f), Quaternion.identity);
            spawned.name = $"VisualHold_{prefabName}";

            var monster = spawned.GetComponent<BaseMonster>();

            if (monster != null)
            {
                monster.ApplyStatus(hold);
            }

            x += SPACING;
        }

        Debug.Log(
            "[StatusVfxVisualHold] 대상을 세웠습니다. 씬 뷰에서 확인 후 플레이를 멈추면 정리됩니다.");
    }
}
