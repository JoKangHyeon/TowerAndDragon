using System;
using System.Collections.Generic;

public enum SaveFailureReason
{
    None,
    InvalidSlot,
    NotSaveablePhase,
    AlreadyRunning,
    CaptureFailed,
    SerializationFailed,
    DiskWriteFailed,

    /// <summary>철인 모드(ironman)에서 이 런에 고정된 슬롯이 아닌 곳에 저장하려 했다.</summary>
    IronmanSlotLocked,
}

public enum SaveLoadFailureReason
{
    None,
    InvalidSlot,
    NotFound,
    FileReadFailed,
    ParseFailed,
    SchemaTooNew,
    SchemaTooOld,
    ValidationFailed,

    /// <summary>봉투 복호화 또는 HMAC 무결성 검증에 실패했다 - 변조되었거나 다른 빌드가 만든 파일이다.</summary>
    IntegrityFailed,
}

public readonly struct SaveResult
{
    public bool IsSuccess { get; }
    public SaveFailureReason Reason { get; }
    public SaveSlotInfo Slot { get; }

    private SaveResult(bool isSuccess, SaveFailureReason reason, SaveSlotInfo slot)
    {
        IsSuccess = isSuccess;
        Reason = reason;
        Slot = slot;
    }

    public static SaveResult Success(SaveSlotInfo slot) =>
        new SaveResult(true, SaveFailureReason.None, slot);

    public static SaveResult Failure(SaveFailureReason reason, int slotIndex) =>
        new SaveResult(false, reason, SaveSlotInfo.Empty(slotIndex));
}

public readonly struct SaveLoadResult
{
    public bool IsSuccess { get; }
    public SaveLoadFailureReason Reason { get; }

    private SaveLoadResult(bool isSuccess, SaveLoadFailureReason reason)
    {
        IsSuccess = isSuccess;
        Reason = reason;
    }

    public static SaveLoadResult Success() => new SaveLoadResult(true, SaveLoadFailureReason.None);

    public static SaveLoadResult Failure(SaveLoadFailureReason reason) =>
        new SaveLoadResult(false, reason);
}

/// <summary>
/// 슬롯 목록 UI가 보는 읽기 전용 요약. 본문(save.sav)이 깨져도 meta.sav만으로 만들 수 있어야
/// "마지막 저장: ... (손상됨)"을 표시할 수 있다 - 메타 파일을 분리한 이유가 이것이다.
/// </summary>
public readonly struct SaveSlotInfo
{
    public int SlotIndex { get; }
    public bool IsEmpty { get; }
    public bool IsCorrupted { get; }

    /// <summary>마지막 저장 시각(UTC). 표시는 SaveTimestampFormatter를 거친다.</summary>
    public DateTimeOffset SavedAtUtc { get; }

    public int DayNumber { get; }
    public int CycleNumber { get; }
    public bool IsAutoSave { get; }
    public int SchemaVersion { get; }
    public int DragonType { get; }

    /// <summary>썸네일 파일이 있는지. 없으면 UI는 빈 프레임을 그린다(구버전 세이브·렌더 실패).</summary>
    public bool HasThumbnail { get; }

    // DTO를 그대로 노출하면 UI가 세이브 파일 포맷에 묶이므로 조회만 열어 둔다.
    private readonly IReadOnlyList<ResourceAmountDto> _resources;

    private SaveSlotInfo(
        int slotIndex,
        bool isEmpty,
        bool isCorrupted,
        DateTimeOffset savedAtUtc,
        int dayNumber,
        int cycleNumber,
        bool isAutoSave,
        int schemaVersion,
        int dragonType,
        bool hasThumbnail,
        IReadOnlyList<ResourceAmountDto> resources)
    {
        SlotIndex = slotIndex;
        IsEmpty = isEmpty;
        IsCorrupted = isCorrupted;
        SavedAtUtc = savedAtUtc;
        DayNumber = dayNumber;
        CycleNumber = cycleNumber;
        IsAutoSave = isAutoSave;
        SchemaVersion = schemaVersion;
        DragonType = dragonType;
        HasThumbnail = hasThumbnail;
        _resources = resources;
    }

    /// <summary>저장 당시의 자원 보유량. 기록이 없는 자원은 0이다.</summary>
    public int GetResourceAmount(ResourceType type)
    {
        if (_resources == null)
        {
            return 0;
        }

        foreach (ResourceAmountDto amount in _resources)
        {
            // meta.sav은 손으로 고칠 수 있고 검증(TryNormalize)을 거치지 않는다 - 원소가 null이면
            // 슬롯 목록을 그리는 도중 NRE가 나므로 여기서 건너뛴다.
            if (amount == null)
            {
                continue;
            }

            if (amount.Type == (int)type)
            {
                return amount.Amount;
            }
        }

        return 0;
    }

    public static SaveSlotInfo Empty(int slotIndex) =>
        new SaveSlotInfo(slotIndex, true, false, default, 0, 0, false, 0, 0, false, null);

    /// <summary>세이브 파일은 있는데 메타조차 읽히지 않는 슬롯. 빈 슬롯과 구분해서 표시한다.</summary>
    public static SaveSlotInfo Corrupted(int slotIndex) =>
        new SaveSlotInfo(slotIndex, false, true, default, 0, 0, false, 0, 0, false, null);

    /// <summary>
    /// 슬롯 번호는 meta.SlotIndex가 아니라 인자로 받는다. 세이브 폴더를 다른 슬롯으로 복사하면
    /// 파일 안의 SlotIndex는 원래 번호로 남아, 목록에 잘못된 번호가 뜨고 삭제가 엉뚱한 폴더를 지운다.
    /// Empty/Corrupted와 같은 근거(= 조회한 폴더 번호)로 판정을 통일한다.
    /// </summary>
    public static SaveSlotInfo FromMeta(
        SaveMetaDto meta,
        int slotIndex,
        bool isCorrupted,
        bool hasThumbnail) =>
        new SaveSlotInfo(
            slotIndex,
            false,
            isCorrupted,
            meta.SavedAtUtc,
            meta.DayNumber,
            meta.CycleNumber,
            meta.IsAutoSave,
            meta.SchemaVersion,
            meta.DragonType,
            hasThumbnail,
            meta.Resources);
}
