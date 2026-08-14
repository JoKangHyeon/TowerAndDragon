using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>빌드 설정에 포함된 씬들을 순회하며 인스펙터 와이어링 누락을 찾아내는 에디터 전용 검사기.
/// 런타임 가드(WiringGuard)는 그 코드가 "실행돼야" 누락을 알려주지만, 이쪽은 플레이하지 않고
/// 씬 데이터만 훑어서 미리 잡는다.
///
/// 검사 항목
///  - 스크립트가 사라진 컴포넌트(Missing Script)
///  - 씬 파일에 남아 있는, 어느 스크립트도 소유하지 않는 m_Script GUID
///  - 직렬화된 오브젝트 참조가 비어 있는 필드(중첩 클래스·배열 원소 포함)
///  - 참조 대상 에셋이 삭제돼 끊어진 참조
///  - UnityEvent의 영속 리스너 중 대상이 비었거나 메서드명이 빈 항목
///
/// 비워둬도 되는 필드는 [WiringOptional]을 붙여 경고로 내린다.
/// 에디터 전용 도구라 CLAUDE.md 5번 예외(문자열·리터럴 상수 규칙 미적용)에 해당한다.</summary>
public static class WiringChecker
{
    public enum IssueKind
    {
        MissingScript,
        UnresolvedScriptGuid,
        UnassignedReference,
        BrokenReference,
        EventListener,
    }

    public enum Severity
    {
        Error,
        Warning,
    }

    public sealed class Issue
    {
        public string ScenePath;
        public string ObjectPath;
        public string ComponentName;
        public string MemberPath;
        public string Tooltip;
        public IssueKind Kind;
        public Severity Level;

        public string Describe()
        {
            string member = string.IsNullOrEmpty(MemberPath) ? string.Empty : "." + MemberPath;
            string tip = string.IsNullOrEmpty(Tooltip) ? string.Empty : "  // " + Tooltip;
            return $"[{Kind}] {ComponentName}{member} @ {ObjectPath}{tip}";
        }
    }

    // Unity가 모든 오브젝트에 붙이는 내부 필드(m_Script, m_PrefabAsset, m_GameObject ...)는 검사 대상이 아니다.
    private const string UNITY_INTERNAL_PREFIX = "m_";

    // UnityEvent 영속 리스너의 직렬화 경로. 이 아래에서는 m_Target/m_MethodName만 의미가 있고
    // m_Arguments.* 는 인자 타입에 따라 정상적으로 비어 있으므로 건너뛴다.
    private const string PERSISTENT_CALLS_PATH = ".m_PersistentCalls.";
    private const string EVENT_TARGET_SUFFIX = ".m_Target";
    private const string EVENT_METHOD_SUFFIX = ".m_MethodName";

    private const string ARRAY_ELEMENT_TOKEN = ".Array.data[";
    private const string SCRIPT_PROPERTY_PATH = "m_Script";

    // 외부 번들 에셋의 스크립트는 우리가 배선하는 대상이 아니다.
    private const string ASSETS_ROOT = "Assets/";
    private const string IMPORTED_ROOT = "Assets/Imported";

    // 씬 YAML의 스크립트 참조. 소유 .meta가 없는 GUID는 새로 클론한 머신에서 Missing Script가 된다.
    private static readonly Regex SCRIPT_GUID_PATTERN =
        new Regex(@"m_Script:\s*\{fileID:\s*-?\d+,\s*guid:\s*([0-9a-f]{32}),\s*type:\s*3\}", RegexOptions.Compiled);

    private const string UNRESOLVED_GUID_HINT =
        "이 GUID를 가진 스크립트가 프로젝트에 없습니다. 씬 YAML의 guid를 올바른 .meta 값으로 고쳐야 합니다.";

    private const BindingFlags FIELD_FLAGS =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    /// <summary>빌드 설정에 등록되고 활성화된 씬 경로들. 순서는 빌드 인덱스 순서 그대로다.</summary>
    public static List<string> CollectBuildScenePaths()
    {
        var paths = new List<string>();
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (!scene.enabled)
            {
                continue;
            }

            paths.Add(scene.path);
        }

        return paths;
    }

    /// <summary>빌드 대상 씬 전체를 검사한다. 이미 열려 있는 씬은 그대로 두고,
    /// 닫혀 있던 씬만 추가(Additive)로 열었다가 다시 닫으므로 편집 중인 변경사항을 잃지 않는다.</summary>
    public static List<Issue> CheckBuildScenes()
    {
        var issues = new List<Issue>();
        foreach (string path in CollectBuildScenePaths())
        {
            issues.AddRange(CheckSceneAtPath(path));
        }

        return issues;
    }

    /// <summary>씬 하나를 경로로 검사한다. 열려 있지 않으면 추가로 열었다가 검사 후 닫는다.</summary>
    public static List<Issue> CheckSceneAtPath(string scenePath)
    {
        // 파일 검사가 먼저다 - 에디터가 이미 들고 있는 씬은 예전 세션의 GUID 리매핑 덕에
        // 깨진 참조가 멀쩡해 보일 수 있어서, 디스크 원본을 따로 봐야 한다.
        List<Issue> fileIssues = CheckSceneFileScriptGuids(scenePath);

        Scene scene = SceneManager.GetSceneByPath(scenePath);
        bool alreadyOpen = scene.IsValid() && scene.isLoaded;
        if (!alreadyOpen)
        {
            scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
        }

        List<Issue> issues = CheckScene(scene);

        if (!alreadyOpen)
        {
            EditorSceneManager.CloseScene(scene, true);
        }

        fileIssues.AddRange(issues);
        return fileIssues;
    }

    /// <summary>씬 파일(YAML)에 적힌 m_Script GUID 중 프로젝트에 실제 스크립트가 없는 것을 찾는다.
    /// 스크립트를 옮기거나 머지가 잘못되면 GUID만 남는데, 이 머신 Library에 예전 매핑이 캐시돼 있으면
    /// 에디터에서는 멀쩡해 보이고 새로 클론한 팀원에게서만 Missing Script로 터진다.</summary>
    public static List<Issue> CheckSceneFileScriptGuids(string scenePath)
    {
        var issues = new List<Issue>();
        if (!File.Exists(scenePath))
        {
            return issues;
        }

        var reported = new HashSet<string>();
        string[] lines = File.ReadAllLines(scenePath);

        for (int i = 0; i < lines.Length; i++)
        {
            Match match = SCRIPT_GUID_PATTERN.Match(lines[i]);
            if (!match.Success)
            {
                continue;
            }

            string guid = match.Groups[1].Value;
            if (!reported.Add(guid))
            {
                continue;
            }

            if (!string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(guid)))
            {
                continue;
            }

            issues.Add(new Issue
            {
                ScenePath = scenePath,
                ObjectPath = $"{scenePath}:{i + 1}",
                ComponentName = $"m_Script guid {guid}",
                Tooltip = UNRESOLVED_GUID_HINT,
                Kind = IssueKind.UnresolvedScriptGuid,
                Level = Severity.Error,
            });
        }

        return issues;
    }

    /// <summary>이미 열려 있는 씬을 검사한다. 비활성 오브젝트도 모두 훑는다.</summary>
    public static List<Issue> CheckScene(Scene scene)
    {
        var issues = new List<Issue>();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return issues;
        }

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform node in root.GetComponentsInChildren<Transform>(true))
            {
                CollectFromGameObject(scene.path, node, issues);
            }
        }

        return issues;
    }

    private static void CollectFromGameObject(string scenePath, Transform node, List<Issue> issues)
    {
        string objectPath = BuildHierarchyPath(node);
        Component[] components = node.GetComponents<Component>();

        for (int i = 0; i < components.Length; i++)
        {
            Component component = components[i];
            if (component == null)
            {
                issues.Add(new Issue
                {
                    ScenePath = scenePath,
                    ObjectPath = objectPath,
                    ComponentName = $"(슬롯 {i})",
                    Kind = IssueKind.MissingScript,
                    Level = Severity.Error,
                });
                continue;
            }

            if (component is MonoBehaviour behaviour && IsProjectScript(behaviour))
            {
                CollectFromBehaviour(scenePath, objectPath, behaviour, issues);
            }
        }
    }

    private static void CollectFromBehaviour(
        string scenePath, string objectPath, MonoBehaviour behaviour, List<Issue> issues)
    {
        Type behaviourType = behaviour.GetType();
        var serialized = new SerializedObject(behaviour);
        SerializedProperty property = serialized.GetIterator();

        while (property.Next(true))
        {
            string path = property.propertyPath;
            if (IsUnityInternalField(path))
            {
                continue;
            }

            bool isEventMember = path.Contains(PERSISTENT_CALLS_PATH);

            if (property.propertyType == SerializedPropertyType.ObjectReference)
            {
                // 영속 리스너 내부에서는 호출 대상만 의미가 있다.
                if (isEventMember && !path.EndsWith(EVENT_TARGET_SUFFIX, StringComparison.Ordinal))
                {
                    continue;
                }

                if (property.objectReferenceValue != null)
                {
                    continue;
                }

                // 인스턴스 ID가 남아 있는데 값이 널이면, 비워둔 게 아니라 대상 에셋이 사라진 것이다.
                bool isBroken = property.objectReferenceInstanceIDValue != 0;
                IssueKind kind = isEventMember
                    ? IssueKind.EventListener
                    : isBroken ? IssueKind.BrokenReference : IssueKind.UnassignedReference;

                issues.Add(BuildIssue(scenePath, objectPath, behaviourType, path, kind, isBroken || isEventMember));
                continue;
            }

            if (property.propertyType == SerializedPropertyType.String
                && isEventMember
                && path.EndsWith(EVENT_METHOD_SUFFIX, StringComparison.Ordinal)
                && string.IsNullOrEmpty(property.stringValue))
            {
                issues.Add(BuildIssue(
                    scenePath, objectPath, behaviourType, path, IssueKind.EventListener, true));
            }
        }
    }

    private static Issue BuildIssue(
        string scenePath,
        string objectPath,
        Type behaviourType,
        string propertyPath,
        IssueKind kind,
        bool alwaysError)
    {
        List<FieldInfo> chain = ResolveFieldChain(behaviourType, propertyPath);
        return new Issue
        {
            ScenePath = scenePath,
            ObjectPath = objectPath,
            ComponentName = behaviourType.Name,
            MemberPath = propertyPath,
            Tooltip = FindTooltip(chain),
            Kind = kind,
            Level = !alwaysError && IsMarkedOptional(chain) ? Severity.Warning : Severity.Error,
        };
    }

    private static bool IsUnityInternalField(string propertyPath)
    {
        if (propertyPath == SCRIPT_PROPERTY_PATH)
        {
            return true;
        }

        // 최상위 세그먼트만 본다 - UnityEvent 내부의 m_Target 등은 사용자 필드 아래에 있으므로 살려야 한다.
        int dot = propertyPath.IndexOf('.');
        string head = dot < 0 ? propertyPath : propertyPath.Substring(0, dot);
        return head.StartsWith(UNITY_INTERNAL_PREFIX, StringComparison.Ordinal);
    }

    private static bool IsProjectScript(MonoBehaviour behaviour)
    {
        MonoScript script = MonoScript.FromMonoBehaviour(behaviour);
        if (script == null)
        {
            return false;
        }

        string path = AssetDatabase.GetAssetPath(script);
        return path.StartsWith(ASSETS_ROOT, StringComparison.Ordinal)
               && !path.StartsWith(IMPORTED_ROOT, StringComparison.Ordinal);
    }

    /// <summary>직렬화 경로를 필드 체인으로 되돌린다.
    /// 예: "_slots.Array.data[0].IconImage" → [_slots, IconImage].
    /// UnityEvent 내부(m_PersistentCalls 이후)는 Unity 내부 구조라 체인에서 잘라낸다.
    /// 해석에 실패하면(경로 중간에 못 찾는 필드가 있으면) 지금까지 찾은 만큼만 돌려준다.</summary>
    private static List<FieldInfo> ResolveFieldChain(Type ownerType, string propertyPath)
    {
        var chain = new List<FieldInfo>();
        int eventIndex = propertyPath.IndexOf(PERSISTENT_CALLS_PATH, StringComparison.Ordinal);
        string path = eventIndex >= 0 ? propertyPath.Substring(0, eventIndex) : propertyPath;

        // "x.Array.data[3]" 를 "x[3]" 형태로 접어 세그먼트 분리를 단순하게 만든다.
        path = path.Replace(ARRAY_ELEMENT_TOKEN, "[");

        Type current = ownerType;
        foreach (string rawSegment in path.Split('.'))
        {
            if (current == null || rawSegment.Length == 0)
            {
                break;
            }

            int bracket = rawSegment.IndexOf('[');
            bool isElement = bracket >= 0;
            string fieldName = isElement ? rawSegment.Substring(0, bracket) : rawSegment;

            FieldInfo field = FindField(current, fieldName);
            if (field == null)
            {
                break;
            }

            chain.Add(field);
            current = isElement ? ResolveElementType(field.FieldType) : field.FieldType;
        }

        return chain;
    }

    private static FieldInfo FindField(Type type, string fieldName)
    {
        // private 필드는 DeclaredOnly로만 잡히므로 상속 계층을 직접 거슬러 올라간다.
        for (Type current = type; current != null; current = current.BaseType)
        {
            FieldInfo field = current.GetField(fieldName, FIELD_FLAGS);
            if (field != null)
            {
                return field;
            }
        }

        return null;
    }

    private static Type ResolveElementType(Type collectionType)
    {
        if (collectionType.IsArray)
        {
            return collectionType.GetElementType();
        }

        if (collectionType.IsGenericType)
        {
            Type[] arguments = collectionType.GetGenericArguments();
            if (arguments.Length == 1)
            {
                return arguments[0];
            }
        }

        return null;
    }

    // 배열 필드에 붙인 [WiringOptional]이 원소까지 덮도록, 체인 전체를 본다.
    private static bool IsMarkedOptional(List<FieldInfo> chain)
    {
        foreach (FieldInfo field in chain)
        {
            if (field.IsDefined(typeof(WiringOptionalAttribute), true))
            {
                return true;
            }
        }

        return false;
    }

    // 리뷰어가 "이건 원래 비워두는 필드인가?"를 바로 판단할 수 있게 툴팁을 함께 싣는다.
    private static string FindTooltip(List<FieldInfo> chain)
    {
        for (int i = chain.Count - 1; i >= 0; i--)
        {
            var tooltip = chain[i].GetCustomAttribute<TooltipAttribute>(true);
            if (tooltip != null && !string.IsNullOrEmpty(tooltip.tooltip))
            {
                return tooltip.tooltip;
            }
        }

        return string.Empty;
    }

    private static string BuildHierarchyPath(Transform node)
    {
        string path = node.name;
        for (Transform parent = node.parent; parent != null; parent = parent.parent)
        {
            path = parent.name + "/" + path;
        }

        return path;
    }
}
