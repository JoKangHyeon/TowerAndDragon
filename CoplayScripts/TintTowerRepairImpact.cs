using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 시간 스킬 연출(<c>FX_TowerRepairImpact</c>)의 화살표를 노랗게 만든다.
///
/// <b>왜 파티클 startColor로는 안 되나</b>: 화살표(<c>Arrows 01/02</c>)의 startColor는 흰색이고
/// (측정: maxColor (0.95,0.95,0.95)) 초록은 머티리얼 <c>M_VFX_Arrow_Vert_01</c>의
/// <c>_ColorTexture</c>(그라디언트 텍스처)에서 나온다. 그래서 파티클 색조를 돌려도 화살표는
/// 초록 그대로다 - 실제로 그렇게 만들었다가 s0(채도 0)만 나오는 것을 확인했다.
///
/// 어떻게 하나: 머티리얼 <b>파생본</b>을 만들어 <c>_EmissionColor</c>를 노랑으로 곱한다.
/// 이 셰이더 계열은 EmissionColor가 최종 색에 곱해지므로 텍스처를 다시 굽지 않아도 색이 바뀐다.
/// (Coplay의 이미지 생성 기능은 사용 금지라 텍스처를 새로 만들 수는 없다.)
///
/// <b>원본 머티리얼을 고치지 않는다</b> - Vefects 팩의 공용 머티리얼이라 생명 타워 명중,
/// 성벽 재생 연출 등 이 머티리얼을 쓰는 모든 이펙트가 함께 노래진다.
///
/// 되돌리려면: git -C Assets/Imported checkout Prefabs/SkillCastOverlay Materials
/// </summary>
public static class TintTowerRepairImpact
{
    private const string PREFAB_PATH =
        "Assets/Imported/Prefabs/SkillCastOverlay/FX_TowerRepairImpact.prefab";

    private const string MATERIAL_FOLDER = "Assets/Imported/Prefabs/SkillCastOverlay/Materials";

    // 설계 §6-1의 시간 색조(42도)를 RGB로. DragonAttributePalette의 Time 값과 같다.
    private static readonly Color TIME_TINT = new Color(0.788f, 0.635f, 0.290f, 1f);

    // Emission은 곱셈이라 그대로 쓰면 어두워진다. 밝기를 되살리려 배수를 준다.
    private const float EMISSION_BOOST = 2.2f;

    private const string EMISSION_COLOR = "_EmissionColor";

    public static string Execute()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PREFAB_PATH);

        if (root == null)
        {
            return "프리팹을 열지 못했습니다: " + PREFAB_PATH;
        }

        var report = new StringBuilder();

        if (!AssetDatabase.IsValidFolder(MATERIAL_FOLDER))
        {
            AssetDatabase.CreateFolder(
                "Assets/Imported/Prefabs/SkillCastOverlay", "Materials");
            report.Append("폴더 생성: ").AppendLine(MATERIAL_FOLDER);
        }

        // 같은 머티리얼을 여러 자식이 공유하므로 한 번만 파생본을 만들어 재사용한다.
        var derivedByOriginal = new Dictionary<Material, Material>();

        try
        {
            foreach (ParticleSystemRenderer renderer in
                root.GetComponentsInChildren<ParticleSystemRenderer>(true))
            {
                Material original = renderer.sharedMaterial;

                if (original == null)
                {
                    continue;
                }

                if (!derivedByOriginal.TryGetValue(original, out Material derived))
                {
                    derived = CreateTinted(original, report);
                    derivedByOriginal.Add(original, derived);
                }

                renderer.sharedMaterial = derived;

                report.Append("  ").Append(renderer.name.PadRight(16))
                    .Append(original.name).Append(" → ").AppendLine(derived.name);
            }

            PrefabUtility.SaveAsPrefabAsset(root, PREFAB_PATH);
            AssetDatabase.SaveAssets();

            report.AppendLine();
            report.Append("머티리얼 파생본 ").Append(derivedByOriginal.Count).AppendLine("개");
            report.Append("색 ").Append(TIME_TINT.ToString("0.##"))
                .Append(" × 밝기 ").Append(EMISSION_BOOST.ToString("0.##")).AppendLine("배");
            report.Append("저장: ").AppendLine(PREFAB_PATH);

            return report.ToString();
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static Material CreateTinted(Material original, StringBuilder report)
    {
        string path = $"{MATERIAL_FOLDER}/{original.name}_Time.mat";

        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        Material derived;

        if (existing != null)
        {
            derived = existing;
        }
        else
        {
            derived = new Material(original);
            AssetDatabase.CreateAsset(derived, path);
        }

        derived.CopyPropertiesFromMaterial(original);

        if (derived.HasProperty(EMISSION_COLOR))
        {
            Color tint = TIME_TINT * EMISSION_BOOST;
            tint.a = 1f;
            derived.SetColor(EMISSION_COLOR, tint);
        }
        else
        {
            report.Append("  ⚠️ ").Append(original.name)
                .Append("에 ").Append(EMISSION_COLOR).AppendLine(" 속성이 없어 색을 바꾸지 못했습니다.");
        }

        EditorUtility.SetDirty(derived);

        return derived;
    }
}
