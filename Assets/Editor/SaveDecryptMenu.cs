using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// [에디터 전용] 세이브가 항상 SaveCrypto 봉투로 저장되므로, 개발 중 내용을 눈으로 보려면
/// 복호화가 필요하다. 이 메뉴는 슬롯별 save.json / meta.json 봉투를 풀어 같은 폴더에
/// save.decrypted.json / meta.decrypted.json 으로 떨군다(원본은 건드리지 않는다).
///
/// 에디터 전용 코드라 문자열·리터럴 규칙 예외 대상이다(CLAUDE.md 커밋규칙 §5).
/// </summary>
public static class SaveDecryptMenu
{
    private const string DUMP_MENU_PATH = "Tools/세이브/슬롯 복호화 덤프";
    private const string OPEN_FOLDER_MENU_PATH = "Tools/세이브/세이브 폴더 열기";
    private const string LOG_PREFIX = "[SaveDecryptMenu] ";

    private const string SAVE_FILE_NAME = "save.sav";
    private const string META_FILE_NAME = "meta.sav";
    private const string LEGACY_SAVE_FILE_NAME = "save.json";
    private const string LEGACY_META_FILE_NAME = "meta.json";
    private const string SAVE_DUMP_NAME = "save.decrypted.json";
    private const string META_DUMP_NAME = "meta.decrypted.json";

    [MenuItem(DUMP_MENU_PATH)]
    public static void DumpAllSlots()
    {
        string root = SaveRootDirectory;

        if (!Directory.Exists(root))
        {
            Debug.LogWarning($"{LOG_PREFIX}세이브 폴더가 없습니다: {root}");
            return;
        }

        int dumped = 0;

        foreach (string slotDir in Directory.GetDirectories(root))
        {
            dumped += DumpOne(
                ResolveSource(slotDir, SAVE_FILE_NAME, LEGACY_SAVE_FILE_NAME),
                Path.Combine(slotDir, SAVE_DUMP_NAME));
            dumped += DumpOne(
                ResolveSource(slotDir, META_FILE_NAME, LEGACY_META_FILE_NAME),
                Path.Combine(slotDir, META_DUMP_NAME));
        }

        Debug.Log($"{LOG_PREFIX}복호화 덤프 {dumped}개 생성. 폴더: {root}");
    }

    [MenuItem(OPEN_FOLDER_MENU_PATH)]
    public static void OpenSaveFolder()
    {
        string root = SaveRootDirectory;
        Directory.CreateDirectory(root);
        EditorUtility.RevealInFinder(root);
    }

    private static string SaveRootDirectory =>
        Path.Combine(Application.persistentDataPath, "Saves");

    // .sav를 우선하고, 없으면 봉투 도입 이전 .json을 본다(슬롯 목록을 아직 안 열어 개명 전인 슬롯).
    private static string ResolveSource(string slotDir, string currentName, string legacyName)
    {
        string current = Path.Combine(slotDir, currentName);
        return File.Exists(current) ? current : Path.Combine(slotDir, legacyName);
    }

    private static int DumpOne(string sourcePath, string dumpPath)
    {
        if (!File.Exists(sourcePath))
        {
            return 0;
        }

        string payload = File.ReadAllText(sourcePath);

        if (!SaveCrypto.IsProtected(payload))
        {
            // 봉투 도입 이전 평문 세이브. 그대로 복사해 둔다(내용은 이미 읽을 수 있다).
            File.WriteAllText(dumpPath, payload);
            Debug.Log($"{LOG_PREFIX}평문(구버전) 그대로 복사: {dumpPath}");
            return 1;
        }

        if (!SaveCrypto.TryUnprotect(payload, out string plainText, out string error))
        {
            Debug.LogError($"{LOG_PREFIX}복호화 실패: {sourcePath}\n{error}");
            return 0;
        }

        File.WriteAllText(dumpPath, plainText);
        Debug.Log($"{LOG_PREFIX}복호화 완료: {dumpPath}");
        return 1;
    }
}
