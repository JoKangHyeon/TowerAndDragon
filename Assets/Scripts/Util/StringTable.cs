using CsvHelper;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

public static class StringTable
{
    static Dictionary<string, string> table;

    // 현재 로드된 언어 코드(예: en_us). LoadLanguage 성공 시 갱신된다.
    public static string CurrentLanguage { get; private set; }

    // 언어가 바뀌면 발화. UI는 이 이벤트를 구독해 표시 중인 텍스트를 다시 그린다.
    public static event System.Action OnLanguageChanged;

    static string _localizationTextLocation;
    static List<string> _localizationList;
    static string LocalizationTextLocation
    {
        get
        {
            if (string.IsNullOrEmpty(_localizationTextLocation))
            {
                LoadLocalizationLoaction();
            }

            return _localizationTextLocation;
        }
    }

    static List<string> LocalizationList
    {
        get
        {
            if (_localizationList == null || _localizationList.Count == 0)
            {
                LoadLocalizationLoaction();
            }
            return _localizationList;
        }
    }

    // 선택 가능한 언어 코드 목록(Localization 폴더의 csv 파일 이름). 설정 창의 언어 드롭다운이 읽는다.
    public static IReadOnlyList<string> AvailableLanguages => LocalizationList;

    public const string c_TableLocation = "";
    public const string c_DefaultLanguage = "en_us";
    public const string c_LanguageFolder = "Localization";
    public const string c_CsvExtension = ".csv";
    public const string c_CsvFilenameFormat = "{0}" + c_CsvExtension;
    public const string c_CsvFileFindQuery = "*" + c_CsvExtension;

    public static void LoadLocalizationLoaction()
    {
        _localizationTextLocation = Path.Combine(Application.streamingAssetsPath, c_LanguageFolder, c_CsvFilenameFormat);
        _localizationList = new List<string>();

        var directoryInfo = new DirectoryInfo( Path.Combine(Application.streamingAssetsPath,c_LanguageFolder));
        foreach (var file in directoryInfo.GetFiles(c_CsvFileFindQuery))
        {
            _localizationList.Add(file.Name.Replace(c_CsvExtension, string.Empty));
        }
    }

    public static string GetString(string key)
    {
        if (table == null)
        {
            LoadLanguage(c_DefaultLanguage);
        }

        if (string.IsNullOrEmpty(key))
            return string.Empty;

        if (!table.ContainsKey(key))
            return key;

        return table[key];
    }

    public static void LoadLanguage(string lang)
    {
        if (table == null)
        {
            table = new Dictionary<string, string>();
        }

        table.Clear();

        var path = string.Format(LocalizationTextLocation, lang);
        string result = null;
        using (StreamReader reader = new StreamReader(path, System.Text.Encoding.UTF8))
        {
            result = reader.ReadToEnd();
        }

        if (result == null)
        {
            Debug.LogError("LANG LOAD FAILED : " + path);
            return;
        }

        var list = LoadCsv<Data>(result);
        foreach (var item in list)
        {
            if (!table.ContainsKey(item.Id))
            {
                table.Add(item.Id, item.String);
            }
            else
            {
                Debug.LogError($"키 중복 : {item.Id}");
            }
        }

        CurrentLanguage = lang;
        OnLanguageChanged?.Invoke();
    }

    // 사용 가능한 언어(LocalizationList)를 순서대로 순환하며 다음 언어로 전환한다.
    // 예: en_us → ko_kr → en_us ... (Localization 폴더의 csv 파일들이 곧 언어 목록)
    public static void CycleLanguage()
    {
        List<string> languages = LocalizationList;
        if (languages == null || languages.Count == 0)
        {
            return;
        }

        int currentIndex = languages.IndexOf(CurrentLanguage);
        int nextIndex = (currentIndex + 1) % languages.Count;
        LoadLanguage(languages[nextIndex]);
    }

    public static List<T> LoadCsv<T>(string csv)
    {
        using (var reader = new StringReader(csv))
        using (var csvReader = new CsvReader(reader, CultureInfo.InvariantCulture))
        {
            var records = csvReader.GetRecords<T>();
            return records.ToList();
        }
    }

    private class Data
    {
        public string Id { get; set; }
        public string String { get; set; }
    }

}
