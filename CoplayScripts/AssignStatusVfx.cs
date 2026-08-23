using UnityEditor;
using UnityEngine;

/// <summary>
/// 상태 지속 연출 프리팹을 StatusEffectSO 애셋에 꽂는다.
///
/// 채우는 것은 타워 화상 하나뿐이다. 어미용 상시 화상(DS_FireBurn, 지속시간 0 = 무한)에 꽂으면
/// 사거리 안의 모든 몬스터에 불이 상시 붙어 전투가 안 보인다 - 비워 두는 것이 기본이다.
/// </summary>
public static class AssignStatusVfx
{
    private const string FIRE_BURN_ASSET_PATH =
        "Assets/Data/TowerData/ElementalTower/Fire/TS_FireBurn.asset";

    private const string FIRE_BURN_VFX_PATH =
        "Assets/Imported/Prefabs/Effects/Status/FX_Status_FireBurn.prefab";

    private const string VFX_PREFAB_PROPERTY = "_activeVfxPrefab";

    public static void Execute()
    {
        var status = AssetDatabase.LoadAssetAtPath<StatusEffectSO>(FIRE_BURN_ASSET_PATH);

        if (status == null)
        {
            Debug.LogError($"[AssignStatusVfx] 상태 애셋을 찾지 못했습니다: {FIRE_BURN_ASSET_PATH}");
            return;
        }

        var vfx = AssetDatabase.LoadAssetAtPath<GameObject>(FIRE_BURN_VFX_PATH);

        if (vfx == null)
        {
            Debug.LogError($"[AssignStatusVfx] 연출 프리팹을 찾지 못했습니다: {FIRE_BURN_VFX_PATH}");
            return;
        }

        var serialized = new SerializedObject(status);
        SerializedProperty property = serialized.FindProperty(VFX_PREFAB_PROPERTY);

        if (property == null)
        {
            Debug.LogError($"[AssignStatusVfx] {VFX_PREFAB_PROPERTY} 필드가 없습니다.");
            return;
        }

        property.objectReferenceValue = vfx;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(status);
        AssetDatabase.SaveAssets();

        Debug.Log($"[AssignStatusVfx] {status.name} ← {vfx.name} 배선 완료.");
    }
}
