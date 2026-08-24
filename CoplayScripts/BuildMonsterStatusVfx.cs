using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// 몬스터에게 걸린 지속 상태(빙결·둔화)를 표시할 프리팹을 만든다.
//
// <b>지금 쓰고 있는 프리팹을 파일 복사해서 사본만 고친다.</b> 원본 팩에서 새로 조합하지 않는다.
// 이유가 둘이다.
//
// 1) 검증한 그림이 그대로 나온다. 후보를 고를 때 Impact_Tower_Ice와 FX_Status_FireBurn을
//    렌더해서 판단했으므로, 만들 때도 그 둘에서 출발해야 같은 결과가 나온다.
//    원본 팩에서 다시 조합하면 배수·계층이 달라져 딴 것이 나온다(실제로 자식 0개짜리를 만들었다).
//
// 2) 쓰고 있는 프리팹을 건드릴 위험이 없다. BuildStatusVfx는 DEFINITIONS를 통째로 순회하며
//    LoadPrefabContents/SaveAsPrefabAsset을 도는 구조라, 새 정의를 하나 얹는 것만으로도
//    기존 산출물이 바뀌는 일이 실제로 일어났다(FX_Status_FireBurn이 "건너뜀"으로 찍혔는데도
//    lengthInSec 5 -> 1.25로 바뀌어 있었다). 여기서는 AssetDatabase.CopyAsset으로 사본을 뜬 뒤
//    그 사본만 연다 - 다른 에셋은 열지도 않는다.
//
// 만드는 것 (Assets/Imported/Prefabs/Effects/Status/):
//   FX_Status_IceFreeze - 빙결(dragon_ice_freeze, 완전 정지, 3초)
//   FX_Status_IceSlow   - 둔화(dragon_ice_slow, 속도 0.5배, 3초)
//
// 이 스크립트는 프리팹만 만든다. 몬스터에 붙이는 것은 MonsterStatusVfx 쪽 작업이다.
public static class BuildMonsterStatusVfx
{
    private const string OUTPUT_FOLDER = "Assets/Imported/Prefabs/Effects/Status";

    // 빙결 바탕. 얼음 타워 명중 이펙트다 - 여기서 솟는 얼음 창(rock1~6)만 남긴다.
    private const string FREEZE_SOURCE = "Assets/Imported/Prefabs/Projectile/Tower/Impact_Tower_Ice.prefab";
    private const string FREEZE_OUTPUT = OUTPUT_FOLDER + "/FX_Status_IceFreeze.prefab";

    // 둔화 바탕. 화상 상태 표시다 - 색조만 파랗게 돌린다.
    private const string SLOW_SOURCE = OUTPUT_FOLDER + "/FX_Status_FireBurn.prefab";
    private const string SLOW_OUTPUT = OUTPUT_FOLDER + "/FX_Status_IceSlow.prefab";

    // 빙결에 남길 자식. 나머지는 터져 나가는 그림이라 뺀다 -
    // 빙결은 터지는 순간이 아니라 갇혀 있는 구간을 그린다.
    private static readonly string[] FREEZE_KEPT_CHILDREN =
    {
        "rock1", "rock2", "rock3", "rock4", "rock5", "rock6"
    };

    // 얼음 하늘색. 얼음 명중의 blue_crack이 (0.33,0.74,1)로 이 각도라 거기에 맞춘다 -
    // 같은 속성이 화면에서 같은 색으로 읽혀야 한다.
    private const float ICE_HUE = 200f;

    // 둔화 크기는 건드리지 않는다 - 바탕인 화상과 같아야 두 상태 표시가 한 세트로 보인다.
    // 한때 0.6배로 줄였다가 물렀다. 화상(폭 0.57) 옆에 둔화(0.33)가 놓이니 같은 계열이 아니라
    // 다른 종류의 표시로 읽혔다.

    public static string Execute()
    {
        var report = new StringBuilder();

        report.AppendLine(BuildFreeze());
        report.AppendLine(BuildSlow());

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        return report.ToString();
    }

    // 빙결: 얼음 창만 남기고 계속 돌린다.
    private static string BuildFreeze()
    {
        var report = new StringBuilder("== 빙결 FX_Status_IceFreeze\n");

        if (!CopyAsset(FREEZE_SOURCE, FREEZE_OUTPUT, report))
        {
            return report.ToString();
        }

        GameObject contents = PrefabUtility.LoadPrefabContents(FREEZE_OUTPUT);

        try
        {
            contents.name = Path.GetFileNameWithoutExtension(FREEZE_OUTPUT);

            int removed = RemoveChildrenExcept(contents, FREEZE_KEPT_CHILDREN, report);

            // 원본이 버스트(loop=0)라 그대로 두면 1.3초에 사라져 3초의 나머지가 빈다.
            // 루트만 켜서는 안 된다 - 실제로 그림을 그리는 것은 자식 이미터다.
            foreach (ParticleSystem ps in contents.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = ps.main;
                main.loop = true;
            }

            PrefabUtility.SaveAsPrefabAsset(contents, FREEZE_OUTPUT);

            report.Append("  자식 ").Append(removed).AppendLine("개 제거, 남은 것을 전부 loop=1로");
            report.Append("  저장: ").AppendLine(FREEZE_OUTPUT);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }

        return report.ToString();
    }

    // 둔화: 화상을 파랗게 돌리기만 한다. 크기는 그대로 둔다.
    private static string BuildSlow()
    {
        var report = new StringBuilder("== 둔화 FX_Status_IceSlow\n");

        if (!CopyAsset(SLOW_SOURCE, SLOW_OUTPUT, report))
        {
            return report.ToString();
        }

        GameObject contents = PrefabUtility.LoadPrefabContents(SLOW_OUTPUT);

        try
        {
            contents.name = Path.GetFileNameWithoutExtension(SLOW_OUTPUT);

            ApplyHue(contents, ICE_HUE);

            PrefabUtility.SaveAsPrefabAsset(contents, SLOW_OUTPUT);

            report.Append("  색조 ").Append(ICE_HUE).AppendLine("도 (크기는 화상 그대로)");
            report.Append("  저장: ").AppendLine(SLOW_OUTPUT);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }

        return report.ToString();
    }

    // 이미 있으면 덮어쓰지 않는다. 손으로 다듬은 값이 얹혀 있을 수 있고,
    // 아직 커밋하지 않았다면 덮어쓰는 순간 사라진다.
    // 다시 만들려면 유니티에서 해당 프리팹을 지우고 이 스크립트를 다시 돌린다.
    private static bool CopyAsset(string source, string destination, StringBuilder report)
    {
        if (File.Exists(destination))
        {
            report.Append("  건너뜀(이미 있음): ").AppendLine(destination);
            return false;
        }

        if (AssetDatabase.LoadAssetAtPath<GameObject>(source) == null)
        {
            report.Append("  바탕을 찾을 수 없습니다: ").AppendLine(source);
            return false;
        }

        if (!AssetDatabase.CopyAsset(source, destination))
        {
            report.Append("  복사 실패: ").Append(source).Append(" -> ").AppendLine(destination);
            return false;
        }

        AssetDatabase.ImportAsset(destination);
        report.Append("  복사: ").Append(Path.GetFileName(source)).Append(" -> ")
              .AppendLine(Path.GetFileName(destination));

        return true;
    }

    // 남길 이름을 뺀 나머지 자식을 지운다.
    //
    // 이름이 아니라 <b>계층</b>을 봐야 한다. 컨테이너 노드를 지우면 그 아래 남겨야 할 자식까지
    // 딸려 사라진다(FX_Crack_Bluerock의 dust_sheet가 그랬다 - 그것 하나를 지웠더니
    // rock1~6이 전부 없어져 자식 0개짜리 프리팹이 나왔다).
    // 그래서 남길 자식의 조상은 지우지 않는다.
    private static int RemoveChildrenExcept(GameObject root, string[] keptNames, StringBuilder report)
    {
        var doomed = new System.Collections.Generic.List<GameObject>();

        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child == root.transform || IsKeptOrAncestorOfKept(child, keptNames))
            {
                continue;
            }

            doomed.Add(child.gameObject);
        }

        int removed = 0;

        foreach (GameObject target in doomed)
        {
            // 앞서 지운 것의 자손이면 이미 사라졌다.
            if (target == null)
            {
                continue;
            }

            UnityEngine.Object.DestroyImmediate(target);
            removed++;
        }

        return removed;
    }

    private static bool IsKeptOrAncestorOfKept(Transform node, string[] keptNames)
    {
        if (Array.IndexOf(keptNames, node.name) >= 0)
        {
            return true;
        }

        foreach (Transform descendant in node.GetComponentsInChildren<Transform>(true))
        {
            if (descendant != node && Array.IndexOf(keptNames, descendant.name) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    // startColor뿐 아니라 Color over Lifetime 그라디언트까지 돌린다.
    // 한쪽만 돌리면 색이 그라디언트에 박힌 자식이 원래 색으로 남는다(화살 궤적에서 겪었다).
    private static void ApplyHue(GameObject root, float hueDegrees)
    {
        float hue = Mathf.Repeat(hueDegrees, 360f) / 360f;

        foreach (ParticleSystem ps in root.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = ps.main;
            main.startColor = ShiftHue(main.startColor, hue);

            var overLifetime = ps.colorOverLifetime;

            if (overLifetime.enabled)
            {
                overLifetime.color = ShiftHue(overLifetime.color, hue);
            }
        }
    }

    private static ParticleSystem.MinMaxGradient ShiftHue(ParticleSystem.MinMaxGradient source, float hue)
    {
        switch (source.mode)
        {
            case ParticleSystemGradientMode.Color:
                return new ParticleSystem.MinMaxGradient(ShiftHue(source.color, hue));

            case ParticleSystemGradientMode.TwoColors:
                return new ParticleSystem.MinMaxGradient(
                    ShiftHue(source.colorMin, hue), ShiftHue(source.colorMax, hue));

            case ParticleSystemGradientMode.Gradient:
                return new ParticleSystem.MinMaxGradient(ShiftHue(source.gradient, hue));

            case ParticleSystemGradientMode.TwoGradients:
                return new ParticleSystem.MinMaxGradient(
                    ShiftHue(source.gradientMin, hue), ShiftHue(source.gradientMax, hue));

            default:
                return source;
        }
    }

    // 알파 키는 손대지 않는다 - 페이드 타이밍이 같이 바뀐다.
    // 채도가 0인 키(흰색·검정)는 ShiftHue가 채도를 유지하므로 저절로 그대로 남는다.
    private static Gradient ShiftHue(Gradient source, float hue)
    {
        if (source == null)
        {
            return null;
        }

        GradientColorKey[] colorKeys = source.colorKeys;

        for (int i = 0; i < colorKeys.Length; i++)
        {
            colorKeys[i].color = ShiftHue(colorKeys[i].color, hue);
        }

        var shifted = new Gradient();
        shifted.mode = source.mode;
        shifted.SetKeys(colorKeys, source.alphaKeys);

        return shifted;
    }

    private static Color ShiftHue(Color source, float hue)
    {
        Color.RGBToHSV(source, out float _, out float saturation, out float value);
        Color shifted = Color.HSVToRGB(hue, saturation, value);
        shifted.a = source.a;

        return shifted;
    }
}
