using System;
using System.IO;
using System.Text;

/// <summary>
/// 세이브 파일 입출력. 프로젝트에서 파일시스템 예외를 결과값으로 바꾸는 유일한 지점이다 -
/// 이 클래스 밖으로는 예외가 새어 나가지 않으므로 호출자는 try/catch를 중복해서 쓰지 않는다.
/// error 문자열은 전부 예외 메시지에서 나오므로 로컬라이즈 대상이 아니다(CLAUDE.md 커밋규칙 §5).
/// </summary>
public static class SaveFileStore
{
    /// <summary>
    /// 임시 파일에 먼저 쓴 뒤 교체한다. 쓰기 도중 크래시가 나도 기존 세이브가 그대로 남는다.
    /// BOM 없는 UTF-8로 쓴다(StringTable이 CSV를 읽는 방식과 맞춘다).
    /// </summary>
    public static bool TryWriteAtomic(string filePath, string contents, out string error) =>
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

    public static bool TryReadAllText(string filePath, out string contents, out string error)
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
