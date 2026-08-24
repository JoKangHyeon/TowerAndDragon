using UnityEngine;
using UnityEditor;
using System.Text;
using System.Collections.Generic;

public static class VerifyStoneMitigation
{
    public static string Execute()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

        var sb = new StringBuilder();

        foreach (string guid in AssetDatabase.FindAssets("t:BabyDragonData"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            BabyDragonData data = AssetDatabase.LoadAssetAtPath<BabyDragonData>(path);
            if (data == null) continue;

            sb.Append(data.name).Append(" [").Append(data.DragonType).Append("] radius=").Append(data.BuffRadius);
            sb.Append(" mitigate={");
            IReadOnlyList<TerrainType> m = data.PenaltyMitigationTerrains;
            if (m != null) for (int i = 0; i < m.Count; i++) { if (i > 0) sb.Append(','); sb.Append(m[i]); }
            sb.Append("} unlock={");
            IReadOnlyList<TerrainType> u = data.ConstructionUnlockTerrains;
            if (u != null) for (int i = 0; i < u.Count; i++) { if (i > 0) sb.Append(','); sb.Append(u[i]); }
            sb.AppendLine("}");
        }

        sb.AppendLine("--- TerrainPenaltyTable ---");
        foreach (string guid in AssetDatabase.FindAssets("t:TerrainPenaltyData"))
        {
            var table = AssetDatabase.LoadAssetAtPath<TerrainPenaltyData>(AssetDatabase.GUIDToAssetPath(guid));
            if (table == null) continue;
            foreach (TerrainType t in System.Enum.GetValues(typeof(TerrainType)))
            {
                TerrainPenaltyEntry e = table.Resolve(t);
                if (e.YieldReductionRatio == 0f && e.TowerAttackSpeedReductionRatio == 0f &&
                    e.WoodUpkeepPerPopulation == 0 && e.StoneUpkeepPerPopulation == 0) continue;
                sb.AppendLine($"{t}: yield-{e.YieldReductionRatio} atkspd-{e.TowerAttackSpeedReductionRatio} wood+{e.WoodUpkeepPerPopulation} stone+{e.StoneUpkeepPerPopulation}");
            }
        }

        return sb.ToString();
    }
}
