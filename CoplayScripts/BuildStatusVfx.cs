using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// 상태·명중 연출용 변형을 만든다. 원본 팩(Eric VFX Studio)은 3D 지면 위에서 터지는 것을 전제로
// 만들어져 있어, 정사영 2D 카메라(회전 0, z=-10)를 쓰는 이 프로젝트에서는 그대로 쓸 수 없다.
//
// 원본은 건드리지 않고 사본만 고친다.
//
// 일회성이냐 지속이냐를 먼저 볼 것. 이 팩의 Crack 계열은 보이는 자식이 전부 loop=0에
// 버스트 방출이라 한 번 터지고 끝난다(= 명중 연출). 반대로 FX_Dust_Fire만 loop=1에 연속 방출이라
// 상태가 걸려 있는 동안 계속 돌릴 수 있다(= 지속 상태 표시). 성격에 맞지 않는 자리에 넣으면
// 일회성은 중간에 끊기고 지속형은 스스로 꺼지지 않는다.
public static class BuildStatusVfx
{
    private const string SOURCE_FOLDER =
        "Assets/Imported/Eric VFX Studio/Game VFX - Ground Crack & Explosion/Prefabs/URP";

    private const string OUTPUT_FOLDER = "Assets/Imported/Prefabs/Effects/Status";
    private const string SORTING_LAYER = "Projectile";

    private sealed class MergedChild
    {
        public string SourceName;
        public string ChildName;

        // 붙인 뒤 바꿔 달 이름. 원본끼리 컨테이너 이름이 같을 때 반드시 필요하다 -
        // Crack_Rock과 Crack_RockAOE는 둘 다 컨테이너가 "Particle System"이라
        // 그대로 두면 ChildScales·RemovedChildren이 어느 쪽을 가리키는지 알 수 없다.
        public string RenameTo;

        // 붙인 사본 안에서 지울 자손. 원본의 다른 자식은 건드리지 않는다.
        public string[] RemovedDescendants = Array.Empty<string>();
    }

    private sealed class ChildScale
    {
        public string ChildName;
        public float Multiplier = 1f;
    }

    private sealed class ChildHeight
    {
        public string ChildName;
        public float LocalY;
    }

    private sealed class ChildGravity
    {
        public string ChildName;
        public float GravityModifier;
    }

    private sealed class BottomAlignment
    {
        // 이 자식의 메시 바닥 높이를 기준으로 삼는다.
        public string ReferenceChild;

        // 바닥을 기준에 맞춰 올리거나 내릴 자식들.
        public string[] Children = Array.Empty<string>();
    }

    private sealed class VfxDefinition
    {
        public string SourceName;
        public string OutputName;
        public string[] RemovedChildren = Array.Empty<string>();

        // 다른 원본에서 자식을 통째로 가져와 붙인다. 두 프리팹의 좋은 부분만 섞을 때 쓴다.
        public MergedChild[] MergedChildren = Array.Empty<MergedChild>();

        // 모든 자식의 X·Y 회전을 0으로 눕힌다. 기본은 끔.
        //
        // 처음에는 켜는 게 맞다고 봤다 - 2D 카메라(회전 0, z=-10)에서 X·Y 기울기는 입자를 화면
        // 안쪽으로 쏘니까. 그런데 실제로 놓고 보면 <b>원본 기울기가 그대로 맞다</b>. 이 게임은
        // 카메라를 기울이는 대신 타일 아트를 아이소메트릭으로 그리는 방식이라, 57도쯤 뒤로 누운
        // 메시가 화면에서 눌려 보이는 것이 곧 "셀 바닥에 놓인" 그림이 된다. 정면으로 세우면
        // 오히려 바닥을 뚫고 선 것처럼 보인다.
        public bool FlattensToScreenPlane;

        // HorizontalBillboard·VerticalBillboard를 Billboard로 바꾼다. 기본은 끔.
        // 위와 같은 이유로 원본 렌더 모드를 함부로 바꾸지 않는다.
        public bool FacesCamera;

        // 자식 하나만 키우거나 줄인다. 두 원본을 섞을 때 반드시 필요하다 -
        // GroundCrack_Blue의 자식은 20~40, Crack_Blue02의 자식은 0.05~2.5로 authoring 기준이
        // 수십 배 다르다. 루트 배수 하나로는 한쪽이 반드시 안 맞는다.
        public ChildScale[] ChildScales = Array.Empty<ChildScale>();

        // 자식의 로컬 Y만 옮긴다. 여러 이미터가 같은 높이에서 솟아야 할 때 쓴다 -
        // 원본은 바위마다 높이가 조금씩 달라(-0.75 ~ -1.0) 어떤 것은 지면 아래에서 올라온다.
        public ChildHeight[] ChildHeights = Array.Empty<ChildHeight>();

        // 메시 바닥을 기준 자식과 같은 높이로 맞춘다.
        //
        // 좌표를 같게 두는 것만으로는 부족하다 - 메시 파티클이 화면에서 어디부터 시작하는지는
        // 피벗 위치와 눕힌 각도가 정한다. Object01은 피벗이 메시 한가운데라 기준점을 맞춰도
        // 절반(3.1~3.3)이 지면 아래로 내려가고, 피벗이 밑동에 있는 Mountain만 지면에서 시작한다.
        public BottomAlignment BottomAlignedChildren;

        // 자식의 중력 계수. 음수면 입자가 위로 뜬다(연기·재).
        //
        // 여기에 적어 두는 이유: 이 스크립트의 산출물은 다시 AssignTowerProjectiles가 복제해
        // Impact_Tower_*를 만들고 게임은 그쪽만 본다. 산출물을 손으로 고치면 이 스크립트를
        // 다시 돌리는 순간 사라지고, 복제본을 손으로 고치면 생성기를 돌리는 순간 사라진다.
        // 유지되어야 하는 값은 반드시 정의에 둔다.
        public ChildGravity[] ChildGravities = Array.Empty<ChildGravity>();

        public float ScaleMultiplier = 1f;
    }

    private static readonly VfxDefinition[] DEFINITIONS =
    {
        // 얼음 명중. 바탕은 FX_Crack_Blue의 URP판이다 - 이름만 FX_GroundCrack_Blue로 다를 뿐
        // 자식 구성(nova·glow·ground·blue_crack·stone·debris·air)이 완전히 같다.
        //
        // 여기에 FX_Crack_Bluerock의 rock1~rock6을 얹는다 - 이 여섯 개가 석영이 솟구치는 그림이다
        // (Mesh Object07 + biang, 수명 1.3초). 처음에는 Crack_Blue02에서 찾다가 헛짚었다.
        //
        // 원본은 여섯 개를 지면(XZ)에 부채꼴로 눕혀 두었는데(회전 57~64도의 X·Y),
        // X·Y를 0으로 눕히면 남은 Z가 그대로 화면 안에서의 부채꼴이 된다(68·104·140·152·156·247도).
        //
        // stone도 지우지 않고 회전만 눕힌다 - 원본이 XZ로 기울여 둬서 돌이 화면 안쪽으로
        // 날아가 버렸을 뿐, 돌이 솟는 그림 자체는 이 자식이 만든다.
        new VfxDefinition
        {
            SourceName = "FX_GroundCrack_Blue",
            OutputName = "FX_Impact_IceCrack",
            MergedChildren = new[]
            {
                new MergedChild { SourceName = "FX_Crack_Bluerock", ChildName = "rock1" },
                new MergedChild { SourceName = "FX_Crack_Bluerock", ChildName = "rock2" },
                new MergedChild { SourceName = "FX_Crack_Bluerock", ChildName = "rock3" },
                new MergedChild { SourceName = "FX_Crack_Bluerock", ChildName = "rock4" },
                new MergedChild { SourceName = "FX_Crack_Bluerock", ChildName = "rock5" },
                new MergedChild { SourceName = "FX_Crack_Bluerock", ChildName = "rock6" }
            },
            // 석영에는 배수를 주지 않는다. 한때 다섯 배로 키웠는데, startSize만 보고 "0.12라 작다"고
            // 판단한 것이 틀렸다 - 메시 파티클의 화면 크기는 <b>메시 바운즈까지</b> 곱해진다.
            // Object07의 긴 축이 2.19라 다섯 배면 석영 하나가 1.5~2.3칸짜리가 됐다.
            // 배수 없이 두면 0.32~0.46칸으로, 균열 위에 얹히는 파편다운 크기가 된다.
            ChildScales = new[]
            {
                new ChildScale { ChildName = "stone", Multiplier = 3f }
            },
            ScaleMultiplier = 0.05f
        },

        // 암석 명중. 얼음과 같은 방식이다 - 바탕 하나에 다른 원본을 얹고, 방향은 원본 그대로 둔다.
        //
        // 바탕 FX_Crack_Rock이 바위 덩이(rock·rock1~3·Mountain)와 지면 균열을 갖고 있고,
        // 여기에 FX_Crack_RockAOE를 통째로 얹어 광역 표시를 더한다 - 암석 타워는 유일한 광역
        // (반경 2)이라 어디까지 맞는지 보여주는 바닥 원이 필요하다.
        //
        // 컨테이너 이름이 양쪽 다 "Particle System"이라 얹는 쪽을 AOE로 바꿔 단다.
        // floor_glow만 빼는 이유는 크기다 - 원본 75.56로 다른 자식(0.8~26)과 자릿수가 달라,
        // 어떤 배수를 줘도 이것 하나가 화면을 덮는다(수명 0.1초짜리 섬광이라 없어도 무방하다).
        new VfxDefinition
        {
            SourceName = "FX_Crack_Rock",
            OutputName = "FX_Impact_StoneCrack",
            MergedChildren = new[]
            {
                new MergedChild
                {
                    SourceName = "FX_Crack_RockAOE",
                    ChildName = "Particle System",
                    RenameTo = "AOE",
                    RemovedDescendants = new[] { "floor_glow" }
                },

                // 날아온 탄이 쪼개지는 순간. Hovl 팩의 "Hit 24 green explosion"을 쓰는데,
                // 이 프로젝트에는 이미 가공본이 Impact_V1_24_green_explosion으로 들어와 있어
                // 원본(Assets/Hovl Studio/...)이 아니라 그쪽을 가져온다.
                //
                // 초록이지만 색 걱정은 없다 - 세트 색조(HueDegrees 280)가 임팩트 전체에 걸리므로
                // 복제 시점에 보라로 돌아간다. 흰색인 Debris는 채도가 0이라 그대로 남는다.
                new MergedChild
                {
                    SourceName = "Assets/Imported/Prefabs/Projectile/AAA_Vol1/Impact_V1_24_green_explosion.prefab",
                    ChildName = "Impact_V1_24_green_explosion",
                    RenameTo = "Burst"
                }
            },
            // rock2만 원본이 유독 크다(0.99칸). 형제 바위들이 0.51~0.56이라 혼자 두 배로 튄다.
            //
            // Burst는 원본 최대 4에 루트 0.08이 곱해져 0.32칸까지 줄어든다. 탄이 쪼개지는 순간을
            // 알릴 만큼은 보여야 하므로 2.5배로 되돌려 0.8칸에 둔다 - 바위(0.5)보다는 크고
            // 광역 표시(3.4)보다는 한참 작은, 가운데서 터지는 크기다.
            ChildScales = new[]
            {
                new ChildScale { ChildName = "rock2", Multiplier = 0.55f },
                new ChildScale { ChildName = "Burst", Multiplier = 2.5f }
            },
            // 바위 네 개가 Mountain과 같은 지면에서 솟게 한다. 좌표를 -0.98로 같게 맞춰 봤지만
            // 소용이 없었다 - Object01은 피벗이 메시 한가운데라 좌표를 맞춰도 절반(3.1~3.3)이
            // 지면 아래로 내려간다. 그래서 좌표가 아니라 메시 바닥을 기준에 맞춘다.
            // X·Z는 건드리지 않아 흩어진 배치는 그대로 남는다.
            BottomAlignedChildren = new BottomAlignment
            {
                ReferenceChild = "Mountain",
                Children = new[] { "rock", "rock1", "rock2", "rock3" }
            },
            // 파열 연기를 위로 띄운다. 대문자 Smoke는 Burst(green_explosion)의 것이고,
            // 소문자 smoke·smoke_out은 AOE 쪽이라 이름이 겹치지 않는다.
            ChildGravities = new[]
            {
                new ChildGravity { ChildName = "Smoke", GravityModifier = -0.22f }
            },
            ScaleMultiplier = 0.08f
        },

        // 불 화상. Glow·Glow_Ground는 머티리얼이 Unity 기본값(Default-ParticleSystem)이라
        // 작가가 칠한 그림이 아니라 흰 덩어리다 - 빼면 Eric 셰이더로 그린 불꽃만 남는다.
        // Pos는 빼면 안 된다 - 불꽃 세 개를 담고 있는 컨테이너라 같이 사라진다(실제로 그렇게 날렸다).
        new VfxDefinition
        {
            SourceName = "FX_Dust_Fire",
            OutputName = "FX_Status_FireBurn",
            RemovedChildren = new[] { "Glow", "Glow_Ground" },
            ScaleMultiplier = 0.18f
        }
    };

    public static string Build()
    {
        EnsureOutputFolder();
        var report = new StringBuilder();

        foreach (VfxDefinition definition in DEFINITIONS)
        {
            string outputPath = $"{OUTPUT_FOLDER}/{definition.OutputName}.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(ResolveSourcePath(definition.SourceName));

            if (root == null)
            {
                throw new InvalidOperationException($"원본을 열 수 없습니다: {definition.SourceName}");
            }

            try
            {
                root.name = definition.OutputName;
                RemoveNamedChildren(root, definition.RemovedChildren);
                MergeChildren(root, definition.MergedChildren);
                FlattenRotations(root, definition.FlattensToScreenPlane);
                ApplyChildScales(root, definition.ChildScales);
                ApplyChildHeights(root, definition.ChildHeights);
                AlignBottoms(root, definition.BottomAlignedChildren);
                ApplyChildGravities(root, definition.ChildGravities);
                root.transform.localScale *= definition.ScaleMultiplier;
                FaceCamera(root, definition.FacesCamera);
                ForceSortingLayer(root);
                PrefabUtility.SaveAsPrefabAsset(root, outputPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            report.AppendLine(Describe(outputPath));
        }

        DeleteStale($"{OUTPUT_FOLDER}/FX_Status_IceSlow.prefab", report);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        return report.ToString();
    }

    // 다른 원본을 열어 지정한 자식만 복제해 붙인다. 원본은 열었다가 그대로 닫으므로 변하지 않는다.
    private static void MergeChildren(GameObject root, IReadOnlyList<MergedChild> merged)
    {
        foreach (MergedChild entry in merged)
        {
            GameObject donor = PrefabUtility.LoadPrefabContents(ResolveSourcePath(entry.SourceName));

            if (donor == null)
            {
                throw new InvalidOperationException($"섞을 원본을 열 수 없습니다: {entry.SourceName}");
            }

            try
            {
                Transform source = donor.GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(t => t.name == entry.ChildName);

                if (source == null)
                {
                    throw new InvalidOperationException(
                        $"{entry.SourceName}에 {entry.ChildName} 자식이 없습니다.");
                }

                var copy = UnityEngine.Object.Instantiate(source.gameObject, root.transform);
                copy.name = string.IsNullOrEmpty(entry.RenameTo) ? entry.ChildName : entry.RenameTo;
                RemoveNamedChildren(copy, entry.RemovedDescendants);
                copy.transform.localPosition = source.localPosition;
                copy.transform.localRotation = source.localRotation;
                copy.transform.localScale = source.localScale;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(donor);
            }
        }
    }

    private static void ApplyChildGravities(GameObject root, IReadOnlyList<ChildGravity> gravities)
    {
        foreach (ChildGravity entry in gravities)
        {
            foreach (ParticleSystem ps in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (ps.gameObject.name != entry.ChildName)
                {
                    continue;
                }

                ParticleSystem.MainModule main = ps.main;
                main.gravityModifier = new ParticleSystem.MinMaxCurve(entry.GravityModifier);
            }
        }
    }

    private static void AlignBottoms(GameObject root, BottomAlignment alignment)
    {
        if (alignment == null)
        {
            return;
        }

        Transform reference = FindChild(root, alignment.ReferenceChild);

        if (reference == null)
        {
            throw new InvalidOperationException($"기준 자식을 찾을 수 없습니다: {alignment.ReferenceChild}");
        }

        float targetBottom = reference.localPosition.y + MeshBottomOffset(reference);

        foreach (string name in alignment.Children)
        {
            Transform child = FindChild(root, name);

            if (child == null)
            {
                throw new InvalidOperationException($"맞출 자식을 찾을 수 없습니다: {name}");
            }

            Vector3 position = child.localPosition;
            child.localPosition = new Vector3(position.x, targetBottom - MeshBottomOffset(child), position.z);
        }
    }

    // 기준점에서 메시 바닥까지의 거리(보통 음수). 회전·스케일·startSize를 모두 먹인 값이다.
    private static float MeshBottomOffset(Transform child)
    {
        var ps = child.GetComponent<ParticleSystem>();
        var renderer = child.GetComponent<ParticleSystemRenderer>();

        if (ps == null || renderer == null || renderer.mesh == null)
        {
            return 0f;
        }

        Bounds bounds = renderer.mesh.bounds;
        Vector3 scale = child.localScale * ps.main.startSize.constant;
        Quaternion rotation = child.localRotation;
        float minY = float.MaxValue;

        for (int corner = 0; corner < 8; corner++)
        {
            var local = new Vector3(
                (corner & 1) == 0 ? bounds.min.x : bounds.max.x,
                (corner & 2) == 0 ? bounds.min.y : bounds.max.y,
                (corner & 4) == 0 ? bounds.min.z : bounds.max.z);

            minY = Mathf.Min(minY, (rotation * Vector3.Scale(local, scale)).y);
        }

        return minY;
    }

    // 이름만 주면 Eric 팩(URP)에서 찾고, 슬래시가 있으면 프로젝트 전체 경로로 본다 -
    // 다른 팩의 이펙트를 섞을 때 필요하다(암석 파열에 쓰는 AAA_Vol1의 폭발이 그렇다).
    private static string ResolveSourcePath(string source)
    {
        return source.Contains("/") ? source : $"{SOURCE_FOLDER}/{source}.prefab";
    }

    private static Transform FindChild(GameObject root, string name)
    {
        return root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
    }

    private static void ApplyChildHeights(GameObject root, IReadOnlyList<ChildHeight> heights)
    {
        foreach (ChildHeight entry in heights)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name != entry.ChildName)
                {
                    continue;
                }

                Vector3 position = child.localPosition;
                child.localPosition = new Vector3(position.x, entry.LocalY, position.z);
            }
        }
    }

    private static void ApplyChildScales(GameObject root, IReadOnlyList<ChildScale> scales)
    {
        foreach (ChildScale entry in scales)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == entry.ChildName)
                {
                    child.localScale *= entry.Multiplier;
                }
            }
        }
    }

    private static void FlattenRotations(GameObject root, bool flattens)
    {
        if (!flattens)
        {
            return;
        }

        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child == root.transform)
            {
                continue;
            }

            Vector3 angles = child.localEulerAngles;
            child.localEulerAngles = new Vector3(0f, 0f, angles.z);
        }
    }

    // HorizontalBillboard는 XZ 평면에 눕는다. 카메라가 +Z를 내려다보는 2D라 그 판은 옆면만 보여
    // 사실상 사라진다. Billboard는 언제나 카메라를 향하므로 그대로 쓸 수 있다.
    private static void FaceCamera(GameObject root, bool faces)
    {
        if (!faces)
        {
            return;
        }

        foreach (ParticleSystemRenderer renderer in root.GetComponentsInChildren<ParticleSystemRenderer>(true))
        {
            if (renderer.renderMode == ParticleSystemRenderMode.HorizontalBillboard ||
                renderer.renderMode == ParticleSystemRenderMode.VerticalBillboard)
            {
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
            }
        }
    }

    private static void ForceSortingLayer(GameObject root)
    {
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            renderer.sortingLayerName = SORTING_LAYER;
        }
    }

    private static void RemoveNamedChildren(GameObject root, IReadOnlyCollection<string> removedNames)
    {
        if (removedNames.Count == 0)
        {
            return;
        }

        HashSet<string> names = removedNames.ToHashSet();
        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);

        for (int index = transforms.Length - 1; index >= 0; index--)
        {
            Transform child = transforms[index];

            if (child != root.transform && names.Contains(child.name))
            {
                UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
        }
    }

    private static void EnsureOutputFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Imported/Prefabs/Effects"))
        {
            AssetDatabase.CreateFolder("Assets/Imported/Prefabs", "Effects");
        }

        if (!AssetDatabase.IsValidFolder(OUTPUT_FOLDER))
        {
            AssetDatabase.CreateFolder("Assets/Imported/Prefabs/Effects", "Status");
        }
    }

    private static void DeleteStale(string path, StringBuilder report)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
        {
            AssetDatabase.DeleteAsset(path);
            report.AppendLine($"삭제: {path} (이름이 바뀌어 남은 옛 산출물)");
        }
    }

    private static string Describe(string path)
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var sb = new StringBuilder();

        bool anyChildLoops = false;

        sb.AppendLine($"== {root.name}  rootScale={root.transform.localScale.x:0.###}");

        foreach (ParticleSystem ps in root.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = ps.main;
            var renderer = ps.GetComponent<ParticleSystemRenderer>();

            // 컨테이너 역할만 하는 시스템은 판정에서 뺀다 - 머티리얼이 Unity 기본값이면
            // 작가가 그린 그림이 아니라 자식을 굴리기 위한 껍데기다.
            bool isDriver = renderer == null || renderer.sharedMaterial == null ||
                renderer.sharedMaterial.name.StartsWith("Default-Particle");

            if (ps.transform != root.transform && !isDriver)
            {
                anyChildLoops |= main.loop;
            }

            sb.Append("   ").Append(ps.gameObject.name.PadRight(18));
            sb.Append(" mode=").Append((renderer == null ? "-" : renderer.renderMode.ToString()).PadRight(10));
            // 자식 자신의 스케일까지 포함해야 화면에서 실제로 얼마만 한지 나온다.
            // 메시 파티클은 메시 바운즈까지 곱해야 실제 화면 크기가 나온다 - startSize만 보면
            // 몇 배씩 빗나간다(석영을 다섯 배로 키웠다가 화면을 덮은 것이 그 때문이다).
            Vector3 bounds = renderer != null && renderer.renderMode == ParticleSystemRenderMode.Mesh &&
                renderer.mesh != null
                ? renderer.mesh.bounds.size
                : Vector3.one;

            Vector3 lossy = ps.transform.lossyScale;
            float longest = Mathf.Max(
                bounds.x * lossy.x, Mathf.Max(bounds.y * lossy.y, bounds.z * lossy.z)) * main.startSize.constant;

            sb.Append(" eff=").Append(longest.ToString("0.###").PadRight(7));
            sb.Append(" loop=").Append(main.loop ? "1" : "0");
            sb.Append(" rot=").Append(ps.transform.localEulerAngles.ToString("0").PadRight(15));
            sb.Append(" layer=").Append(renderer == null ? "-" : renderer.sortingLayerName);
            sb.AppendLine();
        }

        sb.AppendLine(anyChildLoops ? "   -> 지속형(상태 표시용)" : "   -> 일회성(명중 연출용)");
        return sb.ToString();
    }
}
