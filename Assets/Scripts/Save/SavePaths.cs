using System.IO;
using UnityEngine;

/// <summary>
/// 세이브 파일 경로 규칙의 단일 소유자.
/// 파일시스템 문자열은 로컬라이즈 대상이 아니고 이 클래스 밖에서 쓸 일이 없으므로
/// private const로 가둔다 - SettingsService가 PlayerPrefs 키에 쓴 것과 같은 패턴이다.
///
/// 레이아웃:
///   {persistentDataPath}/Saves/slot_00/save.sav          전체 세이브 (단일 진실 원본)
///   {persistentDataPath}/Saves/slot_00/meta.sav          메타만 (본문이 깨져도 읽히는 파생 캐시)
///   {persistentDataPath}/Saves/slot_00/thumbnail.png     점령 현황 썸네일 (파생 캐시)
///   {persistentDataPath}/Saves/slot_00/save.corrupt.sav  파싱·무결성 실패 시 격리본
///
/// save.sav / meta.sav 는 SaveCrypto 봉투("TNDS1."로 시작하는 한 줄)다 - JSON이 아니라서
/// 확장자도 .json이 아니다. thumbnail.png 는 봉투를 거치지 않는 평문 PNG다.
/// 봉투 도입 이전에는 확장자가 .json이었다 - 읽기 경로가 slot 폴더의 save.json/meta.json 을
/// 발견하면 1회 .sav로 개명해 올린다(<see cref="LegacySaveFilePath"/>, SaveSlotQuery).
/// </summary>
public static class SavePaths
{
    private const string SAVE_ROOT_FOLDER = "Saves";
    private const string SLOT_FOLDER_FORMAT = "slot_{0:00}";
    private const string SAVE_FILE_NAME = "save.sav";
    private const string META_FILE_NAME = "meta.sav";
    private const string THUMBNAIL_FILE_NAME = "thumbnail.png";
    private const string CORRUPT_FILE_NAME = "save.corrupt.sav";
    private const string TEMP_EXTENSION = ".tmp";

    // 봉투(SaveCrypto) 도입 전 파일명. 읽기 경로가 발견 시 .sav로 1회 개명한다 - 내용은
    // 건드리지 않는다(봉투 여부는 확장자가 아니라 매직으로 가른다). 다음 저장은 .sav로 나간다.
    private const string LEGACY_SAVE_FILE_NAME = "save.json";
    private const string LEGACY_META_FILE_NAME = "meta.json";

    /// <summary>슬롯 개수. 기획서에 규정이 없어 임시로 정한 값이다.</summary>
    public const int MAX_SLOT_COUNT = 5;

    /// <summary>자동저장 전용 슬롯. 자동저장이 수동 세이브를 덮어쓰지 않도록 예약한다.</summary>
    public const int AUTO_SAVE_SLOT_INDEX = 0;

    public const int INVALID_SLOT_INDEX = -1;

    public static string RootDirectory => Path.Combine(Application.persistentDataPath, SAVE_ROOT_FOLDER);

    public static bool IsValidSlotIndex(int slotIndex) =>
        slotIndex >= 0 && slotIndex < MAX_SLOT_COUNT;

    public static string SlotDirectory(int slotIndex) =>
        Path.Combine(RootDirectory, string.Format(SLOT_FOLDER_FORMAT, slotIndex));

    public static string SaveFilePath(int slotIndex) =>
        Path.Combine(SlotDirectory(slotIndex), SAVE_FILE_NAME);

    public static string MetaFilePath(int slotIndex) =>
        Path.Combine(SlotDirectory(slotIndex), META_FILE_NAME);

    public static string ThumbnailFilePath(int slotIndex) =>
        Path.Combine(SlotDirectory(slotIndex), THUMBNAIL_FILE_NAME);

    public static string CorruptFilePath(int slotIndex) =>
        Path.Combine(SlotDirectory(slotIndex), CORRUPT_FILE_NAME);

    /// <summary>봉투 도입 이전(.json) 본문 경로. 존재하면 읽기 경로가 <see cref="SaveFilePath"/>로 개명한다.</summary>
    public static string LegacySaveFilePath(int slotIndex) =>
        Path.Combine(SlotDirectory(slotIndex), LEGACY_SAVE_FILE_NAME);

    /// <summary>봉투 도입 이전(.json) 메타 경로.</summary>
    public static string LegacyMetaFilePath(int slotIndex) =>
        Path.Combine(SlotDirectory(slotIndex), LEGACY_META_FILE_NAME);

    public static string ToTempPath(string filePath) => filePath + TEMP_EXTENSION;
}
