using CsvHelper;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

public static class StringTable
{
    static Dictionary<string, string> table;

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

    public const string c_TableLocation = "";
    public const string c_DefaultLanguage = "ko_kr";
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

        Debug.Log(result);

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
