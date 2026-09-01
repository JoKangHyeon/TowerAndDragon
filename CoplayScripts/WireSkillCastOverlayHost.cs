using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// M2 - SkillCastOverlayHost를 메인 카메라 자식으로 씬에 배치하고 참조를 채운다.
///
/// <b>씬을 저장하지 않는다.</b> 더티 표시만 하고 저장은 사람이 에디터에서 한다 -
/// Coplay save_scene이 원래 경로 대신 Assets/ 루트에 새 GUID 사본을 만든 전례가 있고,
/// 지금 이 씬에는 다른 작업자의 미저장 변경도 함께 들어 있다.
///
/// 여러 번 돌려도 안전하다 - 이미 있으면 새로 만들지 않고 참조만 다시 채운다.
///
/// 프리팹 모드가 열려 있어도 동작하도록 FindObjectOfType이 아니라 씬 루트에서 직접 훑는다.
/// </summary>
public static class WireSkillCastOverlayHost
{
    private const string HOST_NAME = "SkillCastOverlayHost";
    private const string CAMERA_NAME = "Main Camera";

    private const string OVERLAY_FOLDER = "Assets/Imported/Prefabs/SkillCastOverlay";
    private const string PREFAB_PREFIX = "FX_DragonSkillCastOverlay_";

    // DragonType 열거값과 프리팹 접미사의 짝. 배열 순서가 아니라 이 짝이 정본이다.
    private static readonly (DragonType attribute, string suffix)[] OVERLAYS =
    {
        (DragonType.Ice, "Ice"),
        (DragonType.Fire, "Fire"),
        (DragonType.Time, "Time"),
        (DragonType.Stone, "Stone"),
        (DragonType.Life, "Life")
    };

    public static string Execute()
    {
        var report = new StringBuilder();

        Scene scene = FindEditableScene();

        if (!scene.IsValid())
        {
            return "편집 중인 씬을 찾지 못했습니다.";
        }

        report.Append("씬: ").AppendLine(scene.path);

        GameObject camera = FindRoot(scene, CAMERA_NAME);

        if (camera == null)
        {
            return $"'{CAMERA_NAME}' 오브젝트를 씬 루트에서 찾지 못했습니다.";
        }

        GameObject managerObject = FindComponentOwner<DragonTreeManager>(scene);

        if (managerObject == null)
        {
            return "DragonTreeManager를 가진 오브젝트를 찾지 못했습니다.";
        }

        var dragonTreeManager = managerObject.GetComponent<DragonTreeManager>();

        // 낮 전환·게임 종료에 연출을 즉시 걷기 위한 참조.
        var cycleManager = FindComponent<CycleManager>(scene);
        var gameManager = FindComponent<GameManager>(scene);

        if (cycleManager == null)
        {
            return "CycleManager를 찾지 못했습니다.";
        }

        if (gameManager == null)
        {
            return "GameManager를 찾지 못했습니다.";
        }

        Transform existing = camera.transform.Find(HOST_NAME);
        GameObject host;

        if (existing != null)
        {
            host = existing.gameObject;
            report.AppendLine("기존 호스트를 재사용합니다.");
        }
        else
        {
            host = new GameObject(HOST_NAME);
            Undo.RegisterCreatedObjectUndo(host, "시전 오버레이 호스트 생성");
            host.transform.SetParent(camera.transform, false);
            report.AppendLine("호스트 오브젝트를 새로 만들었습니다.");
        }

        host.transform.localPosition = Vector3.zero;
        host.transform.localRotation = Quaternion.identity;
        host.transform.localScale = Vector3.one;

        var component = host.GetComponent<SkillCastOverlayHost>();

        if (component == null)
        {
            component = Undo.AddComponent<SkillCastOverlayHost>(host);
            report.AppendLine("SkillCastOverlayHost 컴포넌트를 추가했습니다.");
        }

        var serialized = new SerializedObject(component);

        // 스킬 쪽 참조는 없다 - M3부터 SkillTargetingController가 호스트를 직접 부른다(정적 진입점).
        serialized.FindProperty("_dragonTreeManager").objectReferenceValue = dragonTreeManager;
        serialized.FindProperty("_cycleManager").objectReferenceValue = cycleManager;
        serialized.FindProperty("_gameManager").objectReferenceValue = gameManager;

        SerializedProperty overlays = serialized.FindProperty("_overlays");
        overlays.arraySize = OVERLAYS.Length;

        for (int i = 0; i < OVERLAYS.Length; i++)
        {
            (DragonType attribute, string suffix) = OVERLAYS[i];
            string path = $"{OVERLAY_FOLDER}/{PREFAB_PREFIX}{suffix}.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            SerializedProperty element = overlays.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("_attribute").enumValueIndex = (int)attribute;
            element.FindPropertyRelative("_prefab").objectReferenceValue = prefab;

            report.Append("  ").Append(attribute.ToString().PadRight(6))
                .AppendLine(prefab == null ? "프리팹 없음: " + path : path);
        }

        serialized.ApplyModifiedProperties();

        // 플레이 모드에서는 더티 표시가 금지돼 있고, 애초에 그 변경은 플레이 종료 시 버려진다.
        if (Application.isPlaying)
        {
            report.AppendLine();
            report.AppendLine("⚠️ 플레이 모드다 - 이 배선은 플레이를 끝내면 사라진다.");
            report.AppendLine("   영구 반영하려면 플레이를 끝낸 뒤 다시 실행하고 Ctrl+S 할 것.");
            return report.ToString();
        }

        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = host;

        report.AppendLine();
        report.Append("배선 완료: ").Append(camera.name).Append('/').AppendLine(HOST_NAME);
        report.AppendLine("⚠️ 저장하지 않았습니다. 에디터에서 확인 후 Ctrl+S 하세요.");

        return report.ToString();
    }

    // 프리팹 스테이지가 아닌, 실제 편집 중인 씬을 고른다.
    private static Scene FindEditableScene()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);

            if (scene.isLoaded && scene.path.EndsWith(".unity"))
            {
                return scene;
            }
        }

        return default;
    }

    private static GameObject FindRoot(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == name)
            {
                return root;
            }
        }

        return null;
    }

    private static T FindComponent<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T found = root.GetComponentInChildren<T>(true);

            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static GameObject FindComponentOwner<T>(Scene scene) where T : Component
    {
        T found = FindComponent<T>(scene);

        return found == null ? null : found.gameObject;
    }
}
