using CsvHelper;
using CsvHelper.Configuration;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// 구글시트(열 방식) → 언어별 CSV 동기화 에디터 창.
// 한 개의 "데이터 탭"에 [Id | en_us | ko_kr | ...] 처럼 언어를 열로 두고,
// 이 창이 열마다 잘라 StreamingAssets/Localization/{언어}.csv 로 저장한다.
// 기존 탭 방식 툴(CsvDownloader)과 동일한 스프레드시트를 gviz CSV 엔드포인트로 읽는다.
public class LocalizationSyncWindow : EditorWindow
{
    // {0} = 탭(시트) 이름. CsvDownloader와 같은 스프레드시트 ID.
    const string GVIZ_URL_FORMAT =
        "https://docs.google.com/spreadsheets/d/1SfZpNv8lprr_bLrvoxPUAVgBlba_j-2nXu_67xBf5qw/gviz/tq?tqx=out:csv&sheet={0}";

    const string MENU_PATH = "csv/번역 동기화 (열 방식)";
    const string WINDOW_TITLE = "번역 동기화";

    // 데이터 탭의 키 열 이름과, 저장될 CSV의 값 열 헤더.
    const string ID_HEADER = "Id";
    const string STRING_HEADER = "String";

    // 사용자별 설정은 EditorPrefs에 저장(스프레드시트 ID는 코드에 있어 팀 공유).
    const string PREF_SHEET = "LocSync.SheetName";
    const string PREF_IGNORE = "LocSync.IgnoreColumns";
    const string PREF_SELECTED = "LocSync.SelectedLanguages";

    const string DEFAULT_SHEET = "strings";
    const string DEFAULT_IGNORE = "note,comment,memo,desc";

    const char LIST_SEPARATOR = ',';
    const string NEW_LINE = "\n";
    const string QUOTE = "\"";
    const string ESCAPED_QUOTE = "\"\"";
    const char BOM = '﻿';

    string _sheetName;
    string _ignoreColumns;
    readonly List<string> _discovered = new List<string>();
    readonly HashSet<string> _selected = new HashSet<string>();
    string _status;
    bool _statusIsError;

    [MenuItem(MENU_PATH)]
    static void Open()
    {
        var window = GetWindow<LocalizationSyncWindow>(WINDOW_TITLE);
        window.minSize = new Vector2(380f, 340f);
        window.Show();
    }

    void OnEnable()
    {
        _sheetName = EditorPrefs.GetString(PREF_SHEET, DEFAULT_SHEET);
        _ignoreColumns = EditorPrefs.GetString(PREF_IGNORE, DEFAULT_IGNORE);
        _selected.Clear();
        foreach (string lang in SplitList(EditorPrefs.GetString(PREF_SELECTED, string.Empty)))
        {
            _selected.Add(lang);
        }
    }

    void SavePrefs()
    {
        EditorPrefs.SetString(PREF_SHEET, _sheetName);
        EditorPrefs.SetString(PREF_IGNORE, _ignoreColumns);
        EditorPrefs.SetString(PREF_SELECTED, string.Join(LIST_SEPARATOR.ToString(), _selected));
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("구글시트 → CSV 동기화 (열 방식)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "한 탭에 [Id | en_us | ko_kr | ...] 형태로 언어를 열로 둔 뒤,\n" +
            "① 언어 목록 불러오기 → ② 받아올 언어 선택 → ③ 동기화 순서로 사용하세요.\n" +
            "시트는 '링크가 있는 모든 사용자 - 뷰어'로 공유되어 있어야 합니다.",
            MessageType.None);

        EditorGUILayout.Space();
        EditorGUI.BeginChangeCheck();
        _sheetName = EditorGUILayout.TextField(
            new GUIContent("데이터 탭 이름", "모든 언어가 열로 들어있는 탭 이름 (예: strings)"), _sheetName);
        _ignoreColumns = EditorGUILayout.TextField(
            new GUIContent("무시할 열", "언어가 아닌 열 헤더. 쉼표로 구분, 대소문자 무시 (예: note,memo)"), _ignoreColumns);
        if (EditorGUI.EndChangeCheck())
        {
            SavePrefs();
        }

        EditorGUILayout.Space();
        if (GUILayout.Button("① 언어 목록 불러오기 / 새로고침"))
        {
            RefreshLanguages();
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("② 받아올 언어 선택", EditorStyles.boldLabel);
        DrawLanguageToggles();

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(_selected.Count == 0))
        {
            if (GUILayout.Button("③ 선택 언어 동기화 (CSV 저장)", GUILayout.Height(30f)))
            {
                SyncSelected();
            }
        }

        if (!string.IsNullOrEmpty(_status))
        {
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(_status, _statusIsError ? MessageType.Error : MessageType.Info);
        }
    }

    void DrawLanguageToggles()
    {
        if (_discovered.Count == 0)
        {
            EditorGUILayout.HelpBox("먼저 '① 언어 목록 불러오기'를 눌러 시트의 언어 열(헤더)을 읽어오세요.", MessageType.Info);
            return;
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("전체 선택", GUILayout.Width(80f)))
            {
                foreach (string lang in _discovered)
                {
                    _selected.Add(lang);
                }
                SavePrefs();
            }
            if (GUILayout.Button("전체 해제", GUILayout.Width(80f)))
            {
                _selected.Clear();
                SavePrefs();
            }
        }

        foreach (string lang in _discovered)
        {
            bool current = _selected.Contains(lang);
            bool next = EditorGUILayout.ToggleLeft(lang, current);
            if (next == current)
            {
                continue;
            }

            if (next)
            {
                _selected.Add(lang);
            }
            else
            {
                _selected.Remove(lang);
            }
            SavePrefs();
        }
    }

    void RefreshLanguages()
    {
        try
        {
            EditorUtility.DisplayProgressBar(WINDOW_TITLE, "데이터 탭 불러오는 중...", 0.5f);
            string csv = DownloadSheet(_sheetName);
            List<string> langs = ParseLanguageColumns(csv);

            // 이전에 없던 새 언어만 기본 선택에 추가한다. 사용자가 꺼둔 언어는 그대로 존중한다.
            foreach (string lang in langs)
            {
                if (!_discovered.Contains(lang) && !_selected.Contains(lang))
                {
                    _selected.Add(lang);
                }
            }
            _discovered.Clear();
            _discovered.AddRange(langs);
            _selected.RemoveWhere(lang => !_discovered.Contains(lang));
            SavePrefs();

            bool empty = langs.Count == 0;
            SetStatus(empty
                ? "언어 열을 찾지 못했습니다. 첫 행 헤더에 Id 외 언어 열이 있는지 확인하세요."
                : $"불러오기 완료: 언어 {langs.Count}개 — {string.Join(", ", langs)}", empty);
        }
        catch (Exception e)
        {
            SetStatus(BuildErrorMessage(e), true);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            Repaint();
        }
    }

    void SyncSelected()
    {
        try
        {
            EditorUtility.DisplayProgressBar(WINDOW_TITLE, "데이터 탭 불러오는 중...", 0.4f);
            string csv = DownloadSheet(_sheetName);

            EditorUtility.DisplayProgressBar(WINDOW_TITLE, "CSV 저장 중...", 0.8f);
            int savedCount = WriteSelectedCsv(csv);

            AssetDatabase.Refresh();
            SetStatus($"동기화 완료: {savedCount}개 언어 CSV 저장됨.", false);
        }
        catch (Exception e)
        {
            SetStatus(BuildErrorMessage(e), true);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            Repaint();
        }
    }

    static string DownloadSheet(string sheetName)
    {
        if (string.IsNullOrEmpty(sheetName))
        {
            throw new Exception("데이터 탭 이름이 비어 있습니다.");
        }

        string url = string.Format(GVIZ_URL_FORMAT, Uri.EscapeDataString(sheetName));
        using (var web = new System.Net.WebClient())
        {
            web.Encoding = Encoding.UTF8;
            string result = web.DownloadString(url);
            // 일부 응답 앞에 붙는 BOM은 헤더 매칭('Id')을 깨뜨리므로 제거한다.
            if (result.Length > 0 && result[0] == BOM)
            {
                result = result.Substring(1);
            }
            return result;
        }
    }

    // 헤더에서 Id 열을 제외한, 무시 목록에 없는 언어 열 이름들을 뽑는다.
    List<string> ParseLanguageColumns(string csvText)
    {
        HashSet<string> ignore = ParseIgnoreSet(_ignoreColumns);
        using (var reader = new StringReader(csvText))
        using (CsvReader csv = CreateReader(reader))
        {
            string[] headers = ReadHeaders(csv);
            int idIndex = FindIdIndex(headers);
            if (idIndex < 0)
            {
                throw new Exception($"'{ID_HEADER}' 열을 찾을 수 없습니다. 첫 행 헤더를 확인하세요.");
            }

            var langs = new List<string>();
            for (int i = 0; i < headers.Length; i++)
            {
                string header = NormalizeHeader(headers, i);
                if (i == idIndex || header.Length == 0 || ignore.Contains(header.ToLowerInvariant()))
                {
                    continue;
                }
                langs.Add(header);
            }
            return langs;
        }
    }

    // 선택된 언어 열마다 {언어}.csv 파일을 만들고, 저장한 파일 수를 돌려준다.
    int WriteSelectedCsv(string csvText)
    {
        HashSet<string> ignore = ParseIgnoreSet(_ignoreColumns);
        using (var reader = new StringReader(csvText))
        using (CsvReader csv = CreateReader(reader))
        {
            string[] headers = ReadHeaders(csv);
            int idIndex = FindIdIndex(headers);
            if (idIndex < 0)
            {
                throw new Exception($"'{ID_HEADER}' 열을 찾을 수 없습니다.");
            }

            var targetColumnByLang = new Dictionary<string, int>();
            for (int i = 0; i < headers.Length; i++)
            {
                string header = NormalizeHeader(headers, i);
                if (i == idIndex || header.Length == 0 || ignore.Contains(header.ToLowerInvariant()))
                {
                    continue;
                }
                if (_selected.Contains(header))
                {
                    targetColumnByLang[header] = i;
                }
            }
            if (targetColumnByLang.Count == 0)
            {
                throw new Exception("저장할 언어 열이 없습니다. 선택 목록과 시트 헤더를 확인하세요.");
            }

            var builders = new Dictionary<string, StringBuilder>();
            foreach (string lang in targetColumnByLang.Keys)
            {
                var sb = new StringBuilder();
                AppendRow(sb, ID_HEADER, STRING_HEADER);
                builders[lang] = sb;
            }

            while (csv.Read())
            {
                string id = csv.GetField(idIndex);
                if (string.IsNullOrEmpty(id))
                {
                    continue;
                }
                foreach (KeyValuePair<string, int> target in targetColumnByLang)
                {
                    string value = csv.GetField(target.Value) ?? string.Empty;
                    AppendRow(builders[target.Key], id, value);
                }
            }

            WriteFiles(builders);
            return builders.Count;
        }
    }

    static void WriteFiles(Dictionary<string, StringBuilder> builderByLang)
    {
        string dir = Path.Combine(Application.streamingAssetsPath, StringTable.c_LanguageFolder);
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var encoding = new UTF8Encoding(false);
        foreach (KeyValuePair<string, StringBuilder> entry in builderByLang)
        {
            string fileName = string.Format(StringTable.c_CsvFilenameFormat, entry.Key);
            File.WriteAllText(Path.Combine(dir, fileName), entry.Value.ToString(), encoding);
        }
    }

    static CsvReader CreateReader(StringReader reader)
    {
        // 데이터 행의 칸 수가 헤더와 달라도(빈 칸 등) 예외 없이 넘어가도록 관대하게 읽는다.
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null,
            BadDataFound = null,
        };
        return new CsvReader(reader, config);
    }

    static string[] ReadHeaders(CsvReader csv)
    {
        if (!csv.Read() || !csv.ReadHeader())
        {
            throw new Exception("헤더 행을 읽을 수 없습니다. 시트가 비어 있거나 공유되지 않았는지 확인하세요.");
        }
        string[] headers = csv.HeaderRecord ?? Array.Empty<string>();
        if (headers.Length < 2)
        {
            throw new Exception("열이 부족합니다. Id 열과 언어 열이 최소 1개 이상 필요합니다.");
        }
        return headers;
    }

    static string NormalizeHeader(string[] headers, int index)
    {
        return (headers[index] ?? string.Empty).Trim();
    }

    static int FindIdIndex(string[] headers)
    {
        for (int i = 0; i < headers.Length; i++)
        {
            if (string.Equals(NormalizeHeader(headers, i), ID_HEADER, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }
        return -1;
    }

    static void AppendRow(StringBuilder sb, string key, string value)
    {
        sb.Append(Quote(key)).Append(LIST_SEPARATOR).Append(Quote(value)).Append(NEW_LINE);
    }

    static string Quote(string s)
    {
        return QUOTE + (s ?? string.Empty).Replace(QUOTE, ESCAPED_QUOTE) + QUOTE;
    }

    static HashSet<string> ParseIgnoreSet(string csv)
    {
        var set = new HashSet<string>();
        foreach (string part in SplitList(csv))
        {
            set.Add(part.ToLowerInvariant());
        }
        return set;
    }

    static IEnumerable<string> SplitList(string csv)
    {
        if (string.IsNullOrEmpty(csv))
        {
            yield break;
        }
        foreach (string part in csv.Split(LIST_SEPARATOR))
        {
            string trimmed = part.Trim();
            if (trimmed.Length > 0)
            {
                yield return trimmed;
            }
        }
    }

    static string BuildErrorMessage(Exception e)
    {
        if (e is System.Net.WebException)
        {
            return "다운로드 실패: 탭 이름이 맞는지, 시트가 '링크가 있는 모든 사용자 - 뷰어'로 공유됐는지 확인하세요.\n(" + e.Message + ")";
        }
        return "실패: " + e.Message;
    }

    void SetStatus(string message, bool isError)
    {
        _status = message;
        _statusIsError = isError;
    }
}
