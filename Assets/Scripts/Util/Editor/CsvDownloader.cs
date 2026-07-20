using CsvHelper;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class CsvDownloader
{
    const string c_LocalizationBaseUrl = "https://docs.google.com/spreadsheets/d/1SfZpNv8lprr_bLrvoxPUAVgBlba_j-2nXu_67xBf5qw/gviz/tq?tqx=out:csv&sheet={0}";
    const string c_LanguagesSheetName = "langs";
    const string c_LocalizationFolder = "Localization";
    public const string c_CsvExtension = ".csv";
    public const string c_CsvFilenameFormat = "{0}" + c_CsvExtension;


    private struct Languages
    {
        public string Language { get; set; }
    }

    [MenuItem("csv/번역 다운로드")]
    public static void DownloadLocalizingCsv()
    {
        List<Languages> languages = null;

        string url = string.Format(c_LocalizationBaseUrl, c_LanguagesSheetName);
        using (var webClient = new System.Net.WebClient())
        {
            string csvData = webClient.DownloadString(url);

            using (var reader = new StringReader(csvData))
            using (var csvReader = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                var records = csvReader.GetRecords<Languages>();
                languages = records.ToList();
            }
        }

        string localizationDir = Path.Combine(Application.streamingAssetsPath, c_LocalizationFolder);
        if (!Directory.Exists(localizationDir))
            Directory.CreateDirectory(localizationDir);

        foreach (Languages lang in languages)
        {
            url = string.Format(c_LocalizationBaseUrl, lang.Language);
            string fileDir = Path.Combine(localizationDir, string.Format(c_CsvFilenameFormat, lang.Language));

            using (var webClient = new System.Net.WebClient())
            {
                string csvData = webClient.DownloadString(url);
                File.WriteAllText(fileDir, csvData);
            }
        }
        Debug.Log("다운로드 완료");
        AssetDatabase.Refresh();
    }
}
