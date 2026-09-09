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

    /// <summary>슬롯 목록 창을 저장 모드로 열었을 때의 헤더. 불러오기 모드는 TitleLocKeys가 갖는다.</summary>
    public const string SAVE_WINDOW_HEADER = "save_window_header";

    /// <summary>이미 데이터가 있는 슬롯을 처음 눌렀을 때, 타임스탬프 자리에 대신 띄우는 확인 문구.</summary>
    public const string SLOT_OVERWRITE_CONFIRM = "save_slot_overwrite_confirm";

    public const string SLOT_SAVED = "save_slot_saved";

    public const string SAVE_FAIL_GENERIC = "save_fail_generic";
    public const string SAVE_FAIL_PHASE = "save_fail_phase";
    public const string SAVE_FAIL_SERIALIZE = "save_fail_serialize";
    public const string SAVE_FAIL_DISK = "save_fail_disk";

    /// <summary>철인 모드에서 고정 슬롯 외에 저장할 수 없다는 안내.</summary>
    public const string SAVE_FAIL_IRONMAN_SLOT = "save_fail_ironman_slot";

    public const string LOAD_FAIL_GENERIC = "load_fail_generic";
    public const string LOAD_FAIL_NOT_FOUND = "load_fail_not_found";
    public const string LOAD_FAIL_PARSE = "load_fail_parse";
    public const string LOAD_FAIL_SCHEMA = "load_fail_schema";

    /// <summary>봉투 복호화·무결성 검증에 걸려 복원을 거부했을 때. 변조 또는 다른 빌드의 파일이다.</summary>
    public const string LOAD_FAIL_INTEGRITY = "load_fail_integrity";

    /// <summary>내용 검증에 걸려 복원을 거부했을 때. 주로 이 빌드가 모르는 뮤테이터가 담긴 세이브다.</summary>
    public const string LOAD_FAIL_VALIDATION = "load_fail_validation";

    /// <summary>철인 모드에서 게임오버로 세이브를 지웠다는 안내. 조용히 사라지면 버그로 읽힌다.</summary>
    public const string IRONMAN_SAVE_DELETED = "save_ironman_deleted";

    // 실패 사유를 문자열 조합으로 만들지 않고 명시적으로 매핑한다 -
    // 오타와 미등록 키를 컴파일 시점에 드러내기 위해서다(ResearchLocKeys와 같은 이유).
    public static string ResolveSaveFailureLocKey(SaveFailureReason reason)
    {
        return reason switch
        {
            SaveFailureReason.NotSaveablePhase => SAVE_FAIL_PHASE,
            SaveFailureReason.SerializationFailed => SAVE_FAIL_SERIALIZE,
            SaveFailureReason.DiskWriteFailed => SAVE_FAIL_DISK,
            SaveFailureReason.IronmanSlotLocked => SAVE_FAIL_IRONMAN_SLOT,
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
            SaveLoadFailureReason.ValidationFailed => LOAD_FAIL_VALIDATION,
            SaveLoadFailureReason.IntegrityFailed => LOAD_FAIL_INTEGRITY,
            _ => LOAD_FAIL_GENERIC,
        };
    }
}
