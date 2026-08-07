using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 열린 씬의 GuideAnchor를 훑어 "어떤 id가 비었는지"를 플레이 없이 보여준다.
// 앵커 방식은 대상이 계층 곳곳에 흩어져 인스펙터 참조처럼 빈 칸을 한눈에 볼 수 없다 - 그 단점만 이 창으로 메운다.
//
// GuideAnchorBinder가 붙이는 앵커는 런타임에만 생기므로 컴포넌트 검색으로는 잡히지 않는다.
// 그래서 바인더의 표를 따로 읽어 함께 센다 - 안 그러면 HUD가 통째로 안 붙어 있는데 "이상 없음"이 뜬다.
public class GuideAnchorValidatorWindow : EditorWindow
{
    const string MENU_PATH = "TowerAndDragon/Guide/앵커 검증";
    const string WINDOW_TITLE = "앵커 검증";
    const string ID_PROPERTY = "_id";
    const string BINDINGS_PROPERTY = "_bindings";
    const string BINDING_ID_PROPERTY = "Id";
    const string BINDING_TARGET_PROPERTY = "Target";

    // 한 id를 채우고 있는 것 하나. 프리팹에 붙은 앵커든, 바인더가 런타임에 붙일 예정이든 똑같이 센다.
    readonly struct AnchorEntry
    {
        public readonly GameObject Target;
        public readonly bool IsFromBinder;

        public AnchorEntry(GameObject target, bool isFromBinder)
        {
            Target = target;
            IsFromBinder = isFromBinder;
        }

        public string Describe()
        {
            string targetName = Target == null ? "(대상 없음)" : Target.name;
            return IsFromBinder ? $"{targetName} (바인더)" : targetName;
        }
    }

    readonly Dictionary<GuideAnchorId, List<AnchorEntry>> _anchorsById = new();
    readonly List<GuideAnchor> _idNotSet = new();
    readonly List<GuideAnchor> _notRectTransform = new();
    readonly List<GameObject> _binderTargetMissing = new();
    bool _hasScanned;
    Vector2 _scroll;

    [MenuItem(MENU_PATH)]
    static void Open()
    {
        var window = GetWindow<GuideAnchorValidatorWindow>(WINDOW_TITLE);
        window.minSize = new Vector2(420f, 360f);
        window.Show();
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("GuideAnchor 배선 검증", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "열려 있는 씬(및 프리팹 편집 모드의 내용)만 검사합니다.\n" +
            "HUD처럼 프리팹 안에 앵커를 붙였다면, 그 프리팹이 씬에 인스턴스로 올라와 있어야 여기 잡힙니다.\n" +
            "GuideAnchorBinder의 표도 함께 셉니다(런타임에 붙는 앵커라 컴포넌트로는 보이지 않습니다).",
            MessageType.None);

        EditorGUILayout.Space();
        if (GUILayout.Button("열린 씬 스캔", GUILayout.Height(26f)))
        {
            Scan();
        }

        if (!_hasScanned)
        {
            return;
        }

        EditorGUILayout.Space();
        DrawSummary();

        EditorGUILayout.Space();
        using (var scope = new EditorGUILayout.ScrollViewScope(_scroll))
        {
            _scroll = scope.scrollPosition;
            DrawIdRows();
            DrawProblemList("id가 None인 앵커 (어떤 안내도 이걸 찾지 못한다)", _idNotSet);
            DrawProblemList("RectTransform이 아닌 앵커 (딤 구멍을 뚫을 수 없다)", _notRectTransform);
            DrawBinderProblems();
        }
    }

    void DrawSummary()
    {
        int total = 0;
        int filled = 0;
        foreach (GuideAnchorId id in AllIds())
        {
            total++;
            if (_anchorsById.ContainsKey(id))
            {
                filled++;
            }
        }

        EditorGUILayout.LabelField($"id {total}개 중 {filled}개 배선됨", EditorStyles.boldLabel);
    }

    void DrawIdRows()
    {
        foreach (GuideAnchorId id in AllIds())
        {
            if (!_anchorsById.TryGetValue(id, out List<AnchorEntry> anchors))
            {
                DrawRow(id.ToString(), "없음", MessageType.Warning, null);
                continue;
            }

            if (anchors.Count > 1)
            {
                DrawRow(id.ToString(), $"중복 {anchors.Count}개 — {anchors[0].Describe()} …", MessageType.Error,
                    anchors[0].Target);
                continue;
            }

            DrawRow(id.ToString(), anchors[0].Describe(), MessageType.None, anchors[0].Target);
        }
    }

    void DrawProblemList(string title, List<GuideAnchor> anchors)
    {
        if (anchors.Count == 0)
        {
            return;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        foreach (GuideAnchor anchor in anchors)
        {
            DrawRow(anchor.name, "확인 필요", MessageType.Error, anchor.gameObject);
        }
    }

    void DrawBinderProblems()
    {
        if (_binderTargetMissing.Count == 0)
        {
            return;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("대상이 비어 있는 바인더 항목 (등록되지 않는다)", EditorStyles.boldLabel);
        foreach (GameObject binder in _binderTargetMissing)
        {
            DrawRow(binder == null ? "(사라진 바인더)" : binder.name, "확인 필요", MessageType.Error, binder);
        }
    }

    static void DrawRow(string label, string status, MessageType severity, GameObject target)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(label, GUILayout.Width(220f));

            Color previous = GUI.color;
            GUI.color = severity switch
            {
                MessageType.Error => new Color(1f, 0.5f, 0.5f),
                MessageType.Warning => new Color(1f, 0.85f, 0.4f),
                _ => previous,
            };
            EditorGUILayout.LabelField(status);
            GUI.color = previous;

            using (new EditorGUI.DisabledScope(target == null))
            {
                if (GUILayout.Button("선택", GUILayout.Width(48f)) && target != null)
                {
                    Selection.activeGameObject = target;
                    EditorGUIUtility.PingObject(target);
                }
            }
        }
    }

    static IEnumerable<GuideAnchorId> AllIds()
    {
        foreach (GuideAnchorId id in Enum.GetValues(typeof(GuideAnchorId)))
        {
            if (id != GuideAnchorId.None)
            {
                yield return id;
            }
        }
    }

    void Scan()
    {
        _anchorsById.Clear();
        _idNotSet.Clear();
        _notRectTransform.Clear();
        _binderTargetMissing.Clear();

        ScanAnchors();
        ScanBinders();

        _hasScanned = true;
    }

    void ScanAnchors()
    {
        // 닫힌 창 안의 앵커는 비활성이라 Include가 없으면 잡히지 않는다.
        GuideAnchor[] anchors =
            FindObjectsByType<GuideAnchor>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (GuideAnchor anchor in anchors)
        {
            if (!(anchor.transform is RectTransform))
            {
                _notRectTransform.Add(anchor);
            }

            GuideAnchorId id = ReadId(anchor);
            if (id == GuideAnchorId.None)
            {
                _idNotSet.Add(anchor);
                continue;
            }

            AddEntry(id, new AnchorEntry(anchor.gameObject, false));
        }
    }

    // 바인더가 붙일 앵커는 플레이 전에는 존재하지 않으므로, 표를 직렬화 데이터로 읽는다.
    void ScanBinders()
    {
        GuideAnchorBinder[] binders =
            FindObjectsByType<GuideAnchorBinder>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (GuideAnchorBinder binder in binders)
        {
            var serialized = new SerializedObject(binder);
            SerializedProperty bindings = serialized.FindProperty(BINDINGS_PROPERTY);

            if (bindings == null)
            {
                continue;
            }

            for (int i = 0; i < bindings.arraySize; i++)
            {
                SerializedProperty binding = bindings.GetArrayElementAtIndex(i);
                var id = (GuideAnchorId)binding.FindPropertyRelative(BINDING_ID_PROPERTY).intValue;
                var target = binding.FindPropertyRelative(BINDING_TARGET_PROPERTY).objectReferenceValue as RectTransform;

                if (id == GuideAnchorId.None || target == null)
                {
                    _binderTargetMissing.Add(binder.gameObject);
                    continue;
                }

                AddEntry(id, new AnchorEntry(target.gameObject, true));
            }
        }
    }

    void AddEntry(GuideAnchorId id, AnchorEntry entry)
    {
        if (!_anchorsById.TryGetValue(id, out List<AnchorEntry> sameId))
        {
            sameId = new List<AnchorEntry>();
            _anchorsById[id] = sameId;
        }

        sameId.Add(entry);
    }

    // _id는 private [SerializeField]다 - 검증 때문에 공개하지 않고 SerializedObject로 읽는다.
    static GuideAnchorId ReadId(GuideAnchor anchor)
    {
        var serialized = new SerializedObject(anchor);
        SerializedProperty property = serialized.FindProperty(ID_PROPERTY);
        return property == null ? GuideAnchorId.None : (GuideAnchorId)property.intValue;
    }
}
