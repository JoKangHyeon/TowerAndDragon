using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 열린 씬의 GuideAnchor를 훑어 "어떤 id가 비었는지"를 플레이 없이 보여준다.
// 앵커 방식은 대상이 계층 곳곳에 흩어져 인스펙터 참조처럼 빈 칸을 한눈에 볼 수 없다 - 그 단점만 이 창으로 메운다.
public class GuideAnchorValidatorWindow : EditorWindow
{
    const string MENU_PATH = "TowerAndDragon/Guide/앵커 검증";
    const string WINDOW_TITLE = "앵커 검증";
    const string ID_PROPERTY = "_id";

    readonly Dictionary<GuideAnchorId, List<GuideAnchor>> _anchorsById = new();
    readonly List<GuideAnchor> _idNotSet = new();
    readonly List<GuideAnchor> _notRectTransform = new();
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
            "HUD처럼 프리팹 안에 앵커를 붙였다면, 그 프리팹이 씬에 인스턴스로 올라와 있어야 여기 잡힙니다.",
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
            if (!_anchorsById.TryGetValue(id, out List<GuideAnchor> anchors))
            {
                DrawRow(id.ToString(), "없음", MessageType.Warning, null);
                continue;
            }

            if (anchors.Count > 1)
            {
                DrawRow(id.ToString(), $"중복 {anchors.Count}개 — {anchors[0].name} …", MessageType.Error, anchors[0]);
                continue;
            }

            DrawRow(id.ToString(), anchors[0].name, MessageType.None, anchors[0]);
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
            DrawRow(anchor.name, "확인 필요", MessageType.Error, anchor);
        }
    }

    static void DrawRow(string label, string status, MessageType severity, GuideAnchor target)
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
                    Selection.activeGameObject = target.gameObject;
                    EditorGUIUtility.PingObject(target.gameObject);
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

            if (!_anchorsById.TryGetValue(id, out List<GuideAnchor> sameId))
            {
                sameId = new List<GuideAnchor>();
                _anchorsById[id] = sameId;
            }
            sameId.Add(anchor);
        }

        _hasScanned = true;
    }

    // _id는 private [SerializeField]다 - 검증 때문에 공개하지 않고 SerializedObject로 읽는다.
    static GuideAnchorId ReadId(GuideAnchor anchor)
    {
        var serialized = new SerializedObject(anchor);
        SerializedProperty property = serialized.FindProperty(ID_PROPERTY);
        return property == null ? GuideAnchorId.None : (GuideAnchorId)property.intValue;
    }
}
