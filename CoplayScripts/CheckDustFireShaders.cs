using System.Text;
using UnityEditor;
using UnityEngine;

public static class CheckDustFireShaders
{
    private const string PATH =
        "Assets/Imported/Eric VFX Studio/Game VFX - Ground Crack & Explosion/Prefabs/URP/FX_Dust_Fire.prefab";

    public static string Execute()
    {
        var sb = new StringBuilder();
        sb.AppendLine("pipeline=" + (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline == null
            ? "<Built-in>"
            : UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.GetType().Name));

        var root = AssetDatabase.LoadAssetAtPath<GameObject>(PATH);

        foreach (ParticleSystem ps in root.GetComponentsInChildren<ParticleSystem>(true))
        {
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            Material material = renderer == null ? null : renderer.sharedMaterial;

            sb.Append("  ").Append(ps.gameObject.name.PadRight(24));
            sb.Append(" mode=").Append(renderer == null ? "-" : renderer.renderMode.ToString().PadRight(9));
            sb.Append(" active=").Append(ps.gameObject.activeSelf ? "1" : "0");

            if (material == null)
            {
                sb.AppendLine(" mat=<none>");
                continue;
            }

            Shader shader = material.shader;
            sb.Append(" mat=").Append(material.name.PadRight(20));
            sb.Append(" shader=").Append(shader == null ? "<null>" : shader.name);

            // 셰이더가 실제로 컴파일되어 쓸 수 있는지. 깨진 머티리얼은 에러 셰이더로 대체되거나
            // 지원 패스가 하나도 없다.
            if (shader != null)
            {
                sb.Append(" supported=").Append(ShaderUtil.IsShaderPropertyHidden(shader, 0) || true);
                sb.Append(" hasError=").Append(ShaderUtil.ShaderHasError(shader));
                sb.Append(" passes=").Append(shader.passCount);
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }
}
