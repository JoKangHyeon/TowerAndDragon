using System.IO;
using UnityEngine;

/// <summary>
/// 세이브 파일 경로 규칙의 단일 소유자.
/// 파일시스템 문자열은 로컬라이즈 대상이 아니고 이 클래스 밖에서 쓸 일이 없으므로
/// private const로 가둔다 - SettingsService가 PlayerPrefs 키에 쓴 것과 같은 패턴이다.
///
/// 레이아웃:
///   {persistentDataPath}/Saves/slot_00/save.json          전체 세이브 (단일 진실 원본)
///   {persistentDataPath}/Saves/slot_00/meta.json          메타만 (본문이 깨져도 읽히는 파생 캐시)
///   {persistentDataPath}/Saves/slot_00/thumbnail.png      점령 현황 썸네일 (파생 캐시)
///   {persistentDataPath}/Saves/slot_00/save.corrupt.json  파싱 실패 시 격리본
/// </summary>
public static class SavePaths
{
    private const string SAVE_ROOT_FOLDER = "Saves";
    private const string SLOT_FOLDER_FORMAT = "slot_{0:00}";
    private const string SAVE_FILE_NAME = "save.json";
    private const string META_FILE_NAME = "meta.json";
    private const string THUMBNAIL_FILE_NAME = "thumbnail.png";
    private const string CORRUPT_FILE_NAME = "save.corrupt.json";
    private const string TEMP_EXTENSION = ".tmp";

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

    public static string ToTempPath(string filePath) => filePath + TEMP_EXTENSION;
}
