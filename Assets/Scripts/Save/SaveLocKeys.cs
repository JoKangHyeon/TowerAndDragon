// 세이브/이어하기 UI 스크립트 2개 이상이 공유하게 될 로컬 키 (CLAUDE.md 커밋규칙 §3.2).
// 전역 Defines 대신 도메인별 LocKeys 클래스를 두는 기존 관례(ResearchLocKeys, DragonLocKeys)를 따른다.
// 파일 경로·파일명 같은 로컬라이즈 대상이 아닌 문자열은 SavePaths가 private const로 가둔다.
public static class SaveLocKeys
{
    /// <summary>저장 시각 표시 서식. 날짜 표기 순서가 언어마다 달라 번역자가 제어해야 한다.</summary>
    public const string TIMESTAMP_FORMAT = "save_timestamp_format";

    public const string LAST_SAVED_LABEL = "save_last_saved_label";
    public const string SLOT_EMPTY = "save_slot_empty";
    public const string SLOT_CORRUPTED = "save_slot_corrupted";
    public const string SLOT_AUTO_BADGE = "save_slot_auto_badge";
    public const string SLOT_DAY_LABEL = "save_slot_day_label";

    public const string SAVE_FAIL_GENERIC = "save_fail_generic";
    public const string SAVE_FAIL_PHASE = "save_fail_phase";
    public const string SAVE_FAIL_SERIALIZE = "save_fail_serialize";
    public const string SAVE_FAIL_DISK = "save_fail_disk";

    public const string LOAD_FAIL_GENERIC = "load_fail_generic";
    public const string LOAD_FAIL_NOT_FOUND = "load_fail_not_found";
    public const string LOAD_FAIL_PARSE = "load_fail_parse";
    public const string LOAD_FAIL_SCHEMA = "load_fail_schema";

    // 실패 사유를 문자열 조합으로 만들지 않고 명시적으로 매핑한다 -
    // 오타와 미등록 키를 컴파일 시점에 드러내기 위해서다(ResearchLocKeys와 같은 이유).
    public static string ResolveSaveFailureLocKey(SaveFailureReason reason)
    {
        return reason switch
        {
            SaveFailureReason.NotSaveablePhase => SAVE_FAIL_PHASE,
            SaveFailureReason.SerializationFailed => SAVE_FAIL_SERIALIZE,
            SaveFailureReason.DiskWriteFailed => SAVE_FAIL_DISK,
            _ => SAVE_FAIL_GENERIC,
        };
    }

    public static string ResolveLoadFailureLocKey(SaveLoadFailureReason reason)
    {
        return reason switch
        {
            SaveLoadFailureReason.NotFound => LOAD_FAIL_NOT_FOUND,
            SaveLoadFailureReason.ParseFailed => LOAD_FAIL_PARSE,
            SaveLoadFailureReason.FileReadFailed => LOAD_FAIL_PARSE,
            SaveLoadFailureReason.SchemaTooNew => LOAD_FAIL_SCHEMA,
            SaveLoadFailureReason.SchemaTooOld => LOAD_FAIL_SCHEMA,
            _ => LOAD_FAIL_GENERIC,
        };
    }
}
