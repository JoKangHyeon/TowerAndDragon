using System;
using System.IO;
using System.Text;

/// <summary>
/// 세이브 파일 입출력. 프로젝트에서 파일시스템 예외를 결과값으로 바꾸는 유일한 지점이다 -
/// 이 클래스 밖으로는 예외가 새어 나가지 않으므로 호출자는 try/catch를 중복해서 쓰지 않는다.
/// error 문자열은 전부 예외 메시지에서 나오므로 로컬라이즈 대상이 아니다(CLAUDE.md 커밋규칙 §5).
///
/// 세이브 본문·메타 텍스트가 <see cref="SaveCrypto"/> 봉투를 거치는 유일한 지점이기도 하다.
/// 평문으로 쓰는 경로를 밖에 남기지 않으려고 텍스트 원자적 쓰기는 private으로 가두고,
/// public 쓰기 진입점은 <see cref="TryWriteProtectedAtomic"/>(봉투) 하나뿐이다. 읽기는 구버전
/// 평문 세이브를 호출자가 가려야 해서 <see cref="TryReadRawText"/>(원문)만 연다.
/// 썸네일 등 바이너리는 봉투 대상이 아니다.
/// </summary>
public static class SaveFileStore
{
    /// <summary>
    /// 텍스트를 봉투로 감싸 원자적으로 쓴다. 세이브 본문·메타는 반드시 이 경로를 지난다.
    /// </summary>
    public static bool TryWriteProtectedAtomic(string filePath, string contents, out string error)
    {
        if (!SaveCrypto.TryProtect(contents, out string payload, out error))
        {
            return false;
        }

        return TryWriteAtomic(filePath, payload, out error);
    }

    /// <summary>
    /// 파일 원문을 그대로 읽는다(봉투 해제는 호출자 몫). 세이브 본문·메타는 봉투가 아닌
    /// 구버전 평문일 수 있어(<see cref="SaveCrypto.IsProtected"/>) 읽는 쪽이 두 경우를 직접
    /// 가르므로, "무조건 봉투로 해제" 진입점 대신 이 원문 읽기만 공개한다.
    /// </summary>
    public static bool TryReadRawText(string filePath, out string contents, out string error) =>
        TryReadAllText(filePath, out contents, out error);

    /// <summary>
    /// 임시 파일에 먼저 쓴 뒤 교체한다. 쓰기 도중 크래시가 나도 기존 세이브가 그대로 남는다.
    /// BOM 없는 UTF-8로 쓴다(StringTable이 CSV를 읽는 방식과 맞춘다).
    /// </summary>
    private static bool TryWriteAtomic(string filePath, string contents, out string error) =>
        TryWriteAtomic(
            filePath,
            tempPath => File.WriteAllText(tempPath, contents, new UTF8Encoding(false)),
            out error);

    /// <summary>썸네일 등 바이너리 파일용. 원자적 교체 규칙은 텍스트와 동일하다.</summary>
    public static bool TryWriteBytesAtomic(string filePath, byte[] contents, out string error) =>
        TryWriteAtomic(filePath, tempPath => File.WriteAllBytes(tempPath, contents), out error);

    private static bool TryWriteAtomic(string filePath, Action<string> writeToTemp, out string error)
    {
        string tempPath = SavePaths.ToTempPath(filePath);

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            writeToTemp(tempPath);

            // File.Replace는 대상 파일이 없으면 예외를 던지므로 첫 저장은 Move로 처리한다.
            if (File.Exists(filePath))
            {
                File.Replace(tempPath, filePath, null);
            }
            else
            {
                File.Move(tempPath, filePath);
            }

            error = null;
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            TryDeleteFile(tempPath);
            return false;
        }
    }

    public static bool TryReadAllBytes(string filePath, out byte[] contents, out string error)
    {
        contents = null;

        if (!File.Exists(filePath))
        {
            error = $"파일이 없습니다: {filePath}";
            return false;
        }

        try
        {
            contents = File.ReadAllBytes(filePath);
            error = null;
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }

    private static bool TryReadAllText(string filePath, out string contents, out string error)
    {
        contents = null;

        if (!File.Exists(filePath))
        {
            error = $"파일이 없습니다: {filePath}";
            return false;
        }

        try
        {
            contents = File.ReadAllText(filePath, Encoding.UTF8);
            error = null;
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }

    public static bool Exists(string filePath) => File.Exists(filePath);

    public static bool TryDeleteDirectory(string directoryPath, out string error)
    {
        if (!Directory.Exists(directoryPath))
        {
            error = null;
            return true;
        }

        try
        {
            Directory.Delete(directoryPath, true);
            error = null;
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }

    /// <summary>
    /// 파일 하나를 옮긴다(개명). 구버전 .json 세이브를 .sav로 올리는 1회성 경로에서 쓴다 -
    /// 내용은 건드리지 않는다. 대상이 이미 있으면 예외로 실패한다(호출자가 없을 때만 부른다).
    /// </summary>
    public static bool TryRename(string fromPath, string toPath, out string error)
    {
        try
        {
            File.Move(fromPath, toPath);
            error = null;
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }

    /// <summary>
    /// 손상된 세이브를 지우지 않고 옆으로 치운다. 사용자가 제보하거나 손으로 복구할 여지를 남긴다.
    /// 슬롯당 격리본은 1개만 유지한다(이전 격리본은 덮어쓴다).
    /// </summary>
    public static bool TryQuarantine(string filePath, string quarantinePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return false;
            }

            File.Copy(filePath, quarantinePath, true);
            File.Delete(filePath);
            return true;
        }
        catch (Exception exception)
        {
            UnityEngine.Debug.LogWarning($"[SaveFileStore] 손상 파일 격리 실패: {exception.Message}");
            return false;
        }
    }

    private static void TryDeleteFile(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch (Exception exception)
        {
            UnityEngine.Debug.LogWarning($"[SaveFileStore] 임시 파일 정리 실패: {exception.Message}");
        }
    }
}
