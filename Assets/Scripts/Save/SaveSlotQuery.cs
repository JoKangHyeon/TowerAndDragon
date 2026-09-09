using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>
/// 세이브 슬롯 "읽기"의 단일 소유자. 목록 조회·썸네일·삭제처럼 디스크만 만지는 동작을 모은다.
///
/// SaveService에서 떼어낸 이유: SaveService는 GameManager·CycleManager·GridMap 등 인게임
/// 매니저 9개를 [SerializeField]로 물고 있어 타이틀 화면 씬에 둘 수 없다. 반면 조회는
/// SavePaths + SaveFileStore만 있으면 되므로, 씬과 무관한 static으로 두어 타이틀 화면의
/// "이어하기 / 불러오기"가 인게임 매니저 없이도 슬롯을 읽을 수 있게 한다.
/// (SaveLoadRequest와 같은 성격 - 상태를 들고 있지 않으므로 "싱글톤 금지" 관례와 충돌하지 않는다.)
///
/// SaveService는 기존 호출부가 그대로 컴파일되도록 같은 이름의 얇은 위임 메서드를 남겨 둔다.
/// </summary>
public static class SaveSlotQuery
{
    public const int MAX_SLOT_COUNT = SavePaths.MAX_SLOT_COUNT;

    public static IReadOnlyList<SaveSlotInfo> GetSlots()
    {
        var slots = new List<SaveSlotInfo>();

        for (int slotIndex = 0; slotIndex < MAX_SLOT_COUNT; slotIndex++)
        {
            TryGetSlot(slotIndex, out SaveSlotInfo info);
            slots.Add(info);
        }

        return slots;
    }

    public static bool TryGetSlot(int slotIndex, out SaveSlotInfo info)
    {
        info = SaveSlotInfo.Empty(slotIndex);

        if (!SavePaths.IsValidSlotIndex(slotIndex))
        {
            return false;
        }

        RenameLegacyFilesToSav(slotIndex);

        if (!SaveFileStore.Exists(SavePaths.SaveFilePath(slotIndex)))
        {
            return false;
        }

        // 본문이 깨져도 슬롯 목록에 "마지막 저장: ... (손상됨)"을 띄울 수 있도록,
        // 메타는 본문과 별도 파일에서 읽는다. 메타가 없으면 본문에서 뽑아 자가치유한다.
        bool hasThumbnail = SaveFileStore.Exists(SavePaths.ThumbnailFilePath(slotIndex));

        if (TryReadMetaFile(slotIndex, out SaveMetaDto meta))
        {
            bool isCorrupted = !SaveSchema.IsSupportedVersion(meta.SchemaVersion);
            info = SaveSlotInfo.FromMeta(meta, slotIndex, isCorrupted, hasThumbnail);
            return true;
        }

        // 목록 조회에서는 손상 격리를 하지 않는다 - 격리하면 save.sav이 사라져 다음 조회에서
        // "손상됨"이 아니라 "비어 있음"으로 보이고, 사용자가 무슨 일이 있었는지 알 수 없게 된다.
        if (TryReadSave(slotIndex, out SaveGameDto dto, out _, false))
        {
            WriteMetaFile(slotIndex, dto.Meta);

            // TryReadSave가 통과했다는 것만으로 버전이 지원 범위임을 단정하지 않는다 -
            // 두 판정이 갈라지지 않도록 위 분기와 같은 helper를 쓴다.
            info = SaveSlotInfo.FromMeta(
                dto.Meta,
                slotIndex,
                !SaveSchema.IsSupportedVersion(dto.Meta.SchemaVersion),
                hasThumbnail);

            return true;
        }

        // 파일은 있는데 읽히지 않는다. 저장 시각조차 알 수 없지만, 빈 슬롯이 아니라는 사실은 알린다.
        info = SaveSlotInfo.Corrupted(slotIndex);
        return true;
    }

    public static bool HasAnySave => MostRecentSlotIndex != SavePaths.INVALID_SLOT_INDEX;

    public static int MostRecentSlotIndex
    {
        get
        {
            int bestSlotIndex = SavePaths.INVALID_SLOT_INDEX;
            var bestSavedAtUtc = DateTimeOffset.MinValue;

            for (int slotIndex = 0; slotIndex < MAX_SLOT_COUNT; slotIndex++)
            {
                if (!TryGetSlot(slotIndex, out SaveSlotInfo info) || info.IsCorrupted)
                {
                    continue;
                }

                if (info.SavedAtUtc > bestSavedAtUtc)
                {
                    bestSavedAtUtc = info.SavedAtUtc;
                    bestSlotIndex = slotIndex;
                }
            }

            return bestSlotIndex;
        }
    }

    /// <summary>
    /// 슬롯의 점령 현황 썸네일을 UI가 바로 붙일 수 있는 스프라이트로 읽는다.
    /// 부를 때마다 텍스처를 새로 만들므로, 슬롯을 다시 그릴 때는 호출자가 이전 스프라이트와
    /// 그 스프라이트의 texture를 함께 Destroy해야 한다 - 목록을 여닫을 때마다 누적되면
    /// 슬롯 하나당 수백 KB짜리 텍스처가 그대로 새는 자리다.
    /// </summary>
    public static bool TryLoadThumbnail(int slotIndex, out Sprite thumbnail)
    {
        thumbnail = null;

        if (!SavePaths.IsValidSlotIndex(slotIndex))
        {
            return false;
        }

        if (!SaveFileStore.TryReadAllBytes(SavePaths.ThumbnailFilePath(slotIndex), out byte[] pngBytes, out _))
        {
            return false;
        }

        // 실제 크기는 LoadImage가 PNG 헤더를 읽어 다시 잡으므로 여기 값은 의미가 없다.
        var texture = new Texture2D(1, 1, TextureFormat.RGB24, false);

        if (!texture.LoadImage(pngBytes))
        {
            Debug.LogError($"[SaveSlotQuery] 썸네일 디코딩 실패(슬롯 {slotIndex})");
            UnityEngine.Object.Destroy(texture);
            return false;
        }

        thumbnail = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f));

        return true;
    }

    public static bool TryDelete(int slotIndex)
    {
        if (!SavePaths.IsValidSlotIndex(slotIndex))
        {
            return false;
        }

        if (!SaveFileStore.TryDeleteDirectory(SavePaths.SlotDirectory(slotIndex), out string error))
        {
            Debug.LogError($"[SaveSlotQuery] 슬롯 {slotIndex} 삭제 실패: {error}");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 2단계 파싱. DTO로 바로 역직렬화하면 필드 타입이 바뀐 구버전 세이브가 버전 판정 전에
    /// 예외로 터져 원인을 진단할 수 없다. 또한 복원을 시작한 뒤에는 롤백이 불가능하므로
    /// 모든 검증을 여기서 끝낸다.
    /// </summary>
    public static bool TryReadSave(
        int slotIndex,
        out SaveGameDto dto,
        out SaveLoadFailureReason reason,
        bool quarantineOnFailure = true)
    {
        dto = null;

        RenameLegacyFilesToSav(slotIndex);

        string savePath = SavePaths.SaveFilePath(slotIndex);

        if (!SaveFileStore.Exists(savePath))
        {
            reason = SaveLoadFailureReason.NotFound;
            return false;
        }

        if (!SaveFileStore.TryReadRawText(savePath, out string payload, out string readError))
        {
            Debug.LogError($"[SaveSlotQuery] 세이브 읽기 실패(슬롯 {slotIndex}): {readError}");
            reason = SaveLoadFailureReason.FileReadFailed;
            return false;
        }

        if (!TryUnwrapSaveJson(slotIndex, payload, quarantineOnFailure, out string json, out reason))
        {
            return false;
        }

        if (!SaveJson.TryParseObject(json, out JObject root, out string parseError))
        {
            Debug.LogError($"[SaveSlotQuery] 세이브 파싱 실패(슬롯 {slotIndex}): {parseError}");
            Quarantine(slotIndex, quarantineOnFailure);
            reason = SaveLoadFailureReason.ParseFailed;
            return false;
        }

        if (!SaveJson.TryReadSchemaVersion(root, out int schemaVersion))
        {
            Quarantine(slotIndex, quarantineOnFailure);
            reason = SaveLoadFailureReason.ParseFailed;
            return false;
        }

        // 지원 여부는 helper가 판정하고(슬롯 목록의 "손상됨"과 같은 기준), 사유만 여기서 가른다 -
        // 사용자에게 "구버전"과 "더 새 버전"은 대처가 다른 실패다.
        if (!SaveSchema.IsSupportedVersion(schemaVersion))
        {
            reason = schemaVersion > SaveSchema.CURRENT_VERSION
                ? SaveLoadFailureReason.SchemaTooNew
                : SaveLoadFailureReason.SchemaTooOld;

            return false;
        }

        // 여기에 마이그레이션 체인이 들어간다(현재는 구현체 0개 - 검증할 구버전 세이브가 없다).
        // JObject 단계에서 v -> v+1로 끌어올린 뒤 마지막에 한 번만 ToObject 하면
        // 구버전 DTO 클래스를 남기지 않아도 된다.

        if (!SaveJson.TryToObject(root, out dto, out string convertError))
        {
            Debug.LogError($"[SaveSlotQuery] 세이브 변환 실패(슬롯 {slotIndex}): {convertError}");
            Quarantine(slotIndex, quarantineOnFailure);
            reason = SaveLoadFailureReason.ParseFailed;
            return false;
        }

        if (!dto.TryNormalize())
        {
            reason = SaveLoadFailureReason.ValidationFailed;
            return false;
        }

        reason = SaveLoadFailureReason.None;
        return true;
    }

    /// <summary>메타는 파생 캐시다. 본문에서 되살릴 수 있으므로 실패를 무시한다.</summary>
    public static void WriteMetaFile(int slotIndex, SaveMetaDto meta)
    {
        if (SaveJson.TrySerialize(meta, out string metaJson, out _))
        {
            SaveFileStore.TryWriteProtectedAtomic(SavePaths.MetaFilePath(slotIndex), metaJson, out _);
        }
    }

    private static bool TryReadMetaFile(int slotIndex, out SaveMetaDto meta)
    {
        meta = null;

        return SaveFileStore.TryReadRawText(SavePaths.MetaFilePath(slotIndex), out string payload, out _) &&
            TryUnwrapMetaJson(payload, out string json) &&
            SaveJson.TryDeserialize(json, out meta, out _);
    }

    /// <summary>
    /// 세이브 본문 봉투를 풀어 평문 JSON을 얻는다.
    ///
    /// 봉투가 아니면(SaveCrypto 도입 이전 평문 세이브) 에디터·개발 빌드에서만 그대로 통과시킨다 -
    /// 개발 중인 팀원 세이브가 깨지지 않게 하려는 것이고, 다음 저장부터 봉투로 덮인다.
    /// 릴리스 빌드에서 평문을 받아 주면 "평문 JSON을 갖다 놓으면 그대로 먹힌다"가 되어 봉투가
    /// 무의미해지므로, 무결성 실패와 같은 경로(격리 + IntegrityFailed)로 보낸다.
    /// </summary>
    private static bool TryUnwrapSaveJson(
        int slotIndex,
        string payload,
        bool quarantineOnFailure,
        out string json,
        out SaveLoadFailureReason reason)
    {
        json = null;

        if (SaveCrypto.IsProtected(payload))
        {
            if (SaveCrypto.TryUnprotect(payload, out json, out string cryptoError))
            {
                reason = SaveLoadFailureReason.None;
                return true;
            }

            Debug.LogError($"[SaveSlotQuery] 세이브 무결성 검증 실패(슬롯 {slotIndex}): {cryptoError}");
            Quarantine(slotIndex, quarantineOnFailure);
            reason = SaveLoadFailureReason.IntegrityFailed;
            return false;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        json = payload;
        reason = SaveLoadFailureReason.None;
        return true;
#else
        Debug.LogError($"[SaveSlotQuery] 봉투가 아닌 세이브(슬롯 {slotIndex}) - 무결성 실패로 처리합니다.");
        Quarantine(slotIndex, quarantineOnFailure);
        reason = SaveLoadFailureReason.IntegrityFailed;
        return false;
#endif
    }

    // meta.sav은 검증을 거치지 않는 파생 캐시라, 무결성 실패든 구버전 평문이든 조용히 버린다 -
    // 본문에서 되살아난다(TryGetSlot). 릴리스에서 평문 메타를 버리는 것도 본문 규칙과 같은 이유다.
    private static bool TryUnwrapMetaJson(string payload, out string json)
    {
        if (SaveCrypto.IsProtected(payload))
        {
            return SaveCrypto.TryUnprotect(payload, out json, out _);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        json = payload;
        return true;
#else
        json = null;
        return false;
#endif
    }

    // 확장자가 .json이던 시절 저장된 슬롯을 .sav로 1회 개명한다. 조회·로드 양쪽 진입점에서 부른다.
    // 내용(평문/봉투)은 건드리지 않는다 - SaveCrypto가 확장자가 아니라 매직으로 봉투 여부를 가르므로,
    // 개명만으로 읽기 경로가 이어지고 다음 저장은 어차피 .sav로 나간다.
    // .sav가 이미 있으면(= 이미 개명됐거나 새로 저장된 슬롯) 아무것도 하지 않는다.
    private static void RenameLegacyFilesToSav(int slotIndex)
    {
        TryRenameIfCurrentMissing(SavePaths.LegacySaveFilePath(slotIndex), SavePaths.SaveFilePath(slotIndex));
        TryRenameIfCurrentMissing(SavePaths.LegacyMetaFilePath(slotIndex), SavePaths.MetaFilePath(slotIndex));
    }

    private static void TryRenameIfCurrentMissing(string legacyPath, string currentPath)
    {
        if (SaveFileStore.Exists(currentPath) || !SaveFileStore.Exists(legacyPath))
        {
            return;
        }

        if (!SaveFileStore.TryRename(legacyPath, currentPath, out string error))
        {
            Debug.LogWarning($"[SaveSlotQuery] 구버전 세이브 파일명(.json -> .sav) 개명 실패: {error}");
        }
    }

    // 손상된 세이브는 지우지 않고 옆으로 치운다 - 제보와 수동 복구의 여지를 남긴다.
    private static void Quarantine(int slotIndex, bool isEnabled)
    {
        if (!isEnabled)
        {
            return;
        }

        SaveFileStore.TryQuarantine(
            SavePaths.SaveFilePath(slotIndex),
            SavePaths.CorruptFilePath(slotIndex));
    }
}
