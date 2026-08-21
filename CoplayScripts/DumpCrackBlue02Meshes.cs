using System.Text;
using UnityEditor;
using UnityEngine;

public static class DumpCrackBlue02Meshes
{
    private const string PATH =
        "Assets/Imported/Eric VFX Studio/Game VFX - Ground Crack & Explosion/Prefabs/URP/FX_Crack_Bluerock.prefab";

    public static string Execute()
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(PATH);
        var sb = new StringBuilder();

        foreach (ParticleSystem ps in root.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = ps.main;
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            var velocity = ps.velocityOverLifetime;
            var shape = ps.shape;

            sb.Append(ps.gameObject.name.PadRight(16));
            sb.Append(" mode=").Append((renderer == null ? "-" : renderer.renderMode.ToString()).PadRight(10));
            sb.Append(" mesh=").Append((renderer == null || renderer.mesh == null ? "-" : renderer.mesh.name).PadRight(18));
            sb.Append(" mat=").Append((renderer == null || renderer.sharedMaterial == null ? "-" : renderer.sharedMaterial.name).PadRight(22));
            sb.Append(" size=").Append(main.startSize.constant.ToString("0.##").PadRight(6));
            sb.Append(" speed=").Append(main.startSpeed.constant.ToString("0.##").PadRight(6));
            sb.Append(" life=").Append(main.startLifetime.constant.ToString("0.##").PadRight(5));
            sb.Append(" shape=").Append((shape.enabled ? shape.shapeType.ToString() : "off").PadRight(10));
            sb.Append(" velY=").Append(velocity.enabled ? velocity.y.constant.ToString("0.##") : "off");
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
