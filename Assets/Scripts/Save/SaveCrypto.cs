using System;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// 세이브 본문 봉투(envelope)의 단일 소유자. 평문 JSON을 디스크에 그대로 두지 않기 위한
/// 가벼운 변조 방지 계층이다 - 키가 클라이언트에 있으므로 작정한 리버싱은 막지 못하고,
/// 막는 것을 목표로 하지도 않는다. "메모장으로 열어 숫자를 고친다"를 차단하고, 고쳐진
/// 파일이 조용히 먹히는 대신 손상으로 판정되게 하는 것이 전부다.
///
/// 봉투 포맷 (한 줄 ASCII):
///   TNDS1.{base64 IV}.{base64 ciphertext}.{base64 HMAC}
///   - 매직 프리픽스가 포맷 판별자 겸 버전이다. 알고리즘을 바꾸면 TNDS2를 더하고 TNDS1도 읽어 준다.
///   - base64 알파벳에 '.'이 없어 구분자로 안전하다.
///   - 평문 세이브는 '{'로 시작하므로 <see cref="IsProtected"/>가 구버전을 확실히 가른다.
///
/// 암호:
///   - AES-256-CBC/PKCS7. IV는 저장할 때마다 새로 뽑는다(같은 평문도 매번 다른 봉투가 된다).
///   - HMAC-SHA256을 (매직 || IV || ciphertext) 위에 건다(encrypt-then-MAC). 변조는 이 검증이 잡는다.
///   - 키는 PBKDF2-SHA256으로 64바이트를 유도해 앞 32바이트=AES, 뒤 32바이트=HMAC으로 나눈다
///     (두 용도가 같은 키를 쓰지 않게).
///   - 유도 재료는 string이 아니라 byte[]로 둔다 - string 상수는 빌드 바이너리에서 그대로 읽힌다.
///     값 자체는 비밀이 아니고(클라이언트에 있으므로) 진입 장벽을 한 칸 올릴 뿐이다.
///
/// 예외는 밖으로 내보내지 않는다(out string error) - SaveFileStore·SaveJson과 같은 관용구.
/// error 문자열은 예외 메시지·포맷 설명에서 나오므로 로컬라이즈 대상이 아니다(CLAUDE.md 커밋규칙 §5).
///
/// 스레드: 저장의 디스크 쓰기는 스레드풀에서 돈다(SaveService.WriteSlotFiles). Aes·HMACSHA256은
///   호출마다 새로 만들고, 1회 유도해 캐시한 키 바이트(readonly)만 공유한다. Unity API는 건드리지 않는다.
/// </summary>
public static class SaveCrypto
{
    private const string MAGIC = "TNDS1";
    private const char FIELD_SEPARATOR = '.';
    private const int FIELD_COUNT = 4;
    private const int IV_FIELD_INDEX = 1;
    private const int CIPHERTEXT_FIELD_INDEX = 2;
    private const int HMAC_FIELD_INDEX = 3;

    private const int AES_KEY_SIZE_BYTES = 32;
    private const int HMAC_KEY_SIZE_BYTES = 32;
    private const int DERIVED_KEY_SIZE_BYTES = AES_KEY_SIZE_BYTES + HMAC_KEY_SIZE_BYTES;
    private const int AES_IV_SIZE_BYTES = 16;
    private const int AES_KEY_SIZE_BITS = AES_KEY_SIZE_BYTES * 8;

    // PBKDF2 반복 횟수. 1회만 유도해 캐시하므로(EnsureKeys) 저장·로드 비용에는 들어가지 않는다.
    private const int PBKDF2_ITERATIONS = 100_000;

    // 유도 재료. string 상수로 두면 빌드 바이너리에서 strings로 그대로 긁히므로 byte[]로 흩어 둔다.
    private static readonly byte[] PASSPHRASE =
    {
        0x54, 0x6f, 0x77, 0x65, 0x72, 0x44, 0x72, 0x61, 0x67, 0x6f, 0x6e,
        0x2d, 0x73, 0x61, 0x76, 0x65, 0x2d, 0x76, 0x31, 0x2d, 0x9c, 0x2e,
        0x41, 0x7b, 0xd3, 0x08, 0x66, 0xa1, 0x5f, 0xe2, 0x11, 0x90,
    };

    private static readonly byte[] SALT =
    {
        0x73, 0x75, 0x73, 0x65, 0x6f, 0x6e, 0x67, 0x72, 0x6f, 0x6b, 0x2d,
        0x74, 0x6e, 0x64, 0x2d, 0x33, 0x74, 0x65, 0x61, 0x6d, 0xa7, 0x4c,
        0xbb, 0x19, 0x02, 0xf5, 0x6d, 0x88, 0xc0, 0x3a, 0x7e, 0xd1,
    };

    private static readonly byte[] MAGIC_BYTES = Encoding.ASCII.GetBytes(MAGIC);
    private static readonly string MAGIC_PREFIX = MAGIC + FIELD_SEPARATOR;
    private static readonly UTF8Encoding UTF8_NO_BOM = new UTF8Encoding(false);
    private static readonly char[] SEPARATOR_SPLIT = { FIELD_SEPARATOR };
    private static readonly object DERIVE_LOCK = new object();

    // PBKDF2는 저장·로드마다 돌릴 이유가 없다 - 첫 사용에 1회 유도해 캐시한다.
    private static byte[] _aesKey;
    private static byte[] _hmacKey;

    /// <summary>이 문자열이 봉투인지(매직 프리픽스로만 판정). 평문 구버전 세이브를 가르는 데 쓴다.</summary>
    public static bool IsProtected(string payload) =>
        payload != null && payload.StartsWith(MAGIC_PREFIX, StringComparison.Ordinal);

    public static bool TryProtect(string plainText, out string payload, out string error)
    {
        payload = null;

        if (plainText == null)
        {
            error = "평문이 null입니다.";
            return false;
        }

        try
        {
            EnsureKeys();

            byte[] plainBytes = UTF8_NO_BOM.GetBytes(plainText);
            byte[] iv = new byte[AES_IV_SIZE_BYTES];

            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(iv);
            }

            byte[] cipherBytes = Transform(iv, plainBytes, encrypt: true);
            byte[] mac = ComputeMac(iv, cipherBytes);

            var builder = new StringBuilder();
            builder.Append(MAGIC);
            builder.Append(FIELD_SEPARATOR);
            builder.Append(Convert.ToBase64String(iv));
            builder.Append(FIELD_SEPARATOR);
            builder.Append(Convert.ToBase64String(cipherBytes));
            builder.Append(FIELD_SEPARATOR);
            builder.Append(Convert.ToBase64String(mac));

            payload = builder.ToString();
            error = null;
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }

    public static bool TryUnprotect(string payload, out string plainText, out string error)
    {
        plainText = null;

        if (!IsProtected(payload))
        {
            error = "봉투 매직이 없습니다.";
            return false;
        }

        string[] fields = payload.Split(SEPARATOR_SPLIT);
        if (fields.Length != FIELD_COUNT)
        {
            error = $"봉투 필드 수가 {fields.Length}개입니다(기대 {FIELD_COUNT}개).";
            return false;
        }

        try
        {
            EnsureKeys();

            byte[] iv = Convert.FromBase64String(fields[IV_FIELD_INDEX]);
            byte[] cipherBytes = Convert.FromBase64String(fields[CIPHERTEXT_FIELD_INDEX]);
            byte[] mac = Convert.FromBase64String(fields[HMAC_FIELD_INDEX]);

            if (iv.Length != AES_IV_SIZE_BYTES)
            {
                error = $"IV 길이가 {iv.Length}바이트입니다(기대 {AES_IV_SIZE_BYTES}).";
                return false;
            }

            // 복호화 전에 무결성을 먼저 확인한다(encrypt-then-MAC). 변조된 ciphertext를 AES에
            // 넣어 패딩 예외로 터뜨리는 것보다 여기서 명시적으로 가르는 편이 진단하기 쉽다.
            byte[] expectedMac = ComputeMac(iv, cipherBytes);
            if (!ConstantTimeEquals(mac, expectedMac))
            {
                error = "무결성 검증 실패 - 세이브가 변조되었거나 다른 빌드가 만든 파일입니다.";
                return false;
            }

            byte[] plainBytes = Transform(iv, cipherBytes, encrypt: false);
            plainText = UTF8_NO_BOM.GetString(plainBytes);
            error = null;
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }

    private static byte[] Transform(byte[] iv, byte[] input, bool encrypt)
    {
        using (var aes = Aes.Create())
        {
            aes.KeySize = AES_KEY_SIZE_BITS;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = _aesKey;
            aes.IV = iv;

            using (ICryptoTransform transform = encrypt ? aes.CreateEncryptor() : aes.CreateDecryptor())
            {
                return transform.TransformFinalBlock(input, 0, input.Length);
            }
        }
    }

    private static byte[] ComputeMac(byte[] iv, byte[] cipherBytes)
    {
        using (var hmac = new HMACSHA256(_hmacKey))
        {
            hmac.TransformBlock(MAGIC_BYTES, 0, MAGIC_BYTES.Length, null, 0);
            hmac.TransformBlock(iv, 0, iv.Length, null, 0);
            hmac.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
            return hmac.Hash;
        }
    }

    private static void EnsureKeys()
    {
        if (_aesKey != null && _hmacKey != null)
        {
            return;
        }

        lock (DERIVE_LOCK)
        {
            if (_aesKey != null && _hmacKey != null)
            {
                return;
            }

            using (var kdf = new Rfc2898DeriveBytes(
                PASSPHRASE, SALT, PBKDF2_ITERATIONS, HashAlgorithmName.SHA256))
            {
                byte[] derived = kdf.GetBytes(DERIVED_KEY_SIZE_BYTES);

                var aesKey = new byte[AES_KEY_SIZE_BYTES];
                var hmacKey = new byte[HMAC_KEY_SIZE_BYTES];
                Buffer.BlockCopy(derived, 0, aesKey, 0, AES_KEY_SIZE_BYTES);
                Buffer.BlockCopy(derived, AES_KEY_SIZE_BYTES, hmacKey, 0, HMAC_KEY_SIZE_BYTES);

                _aesKey = aesKey;
                _hmacKey = hmacKey;
            }
        }
    }

    // 조기 반환 없이 전체를 훑는다 - 타이밍으로 MAC을 한 바이트씩 맞추는 길을 막는다.
    // (.NET Framework 프로필에는 CryptographicOperations.FixedTimeEquals가 없어 직접 구현한다.)
    private static bool ConstantTimeEquals(byte[] a, byte[] b)
    {
        if (a == null || b == null || a.Length != b.Length)
        {
            return false;
        }

        int difference = 0;
        for (int i = 0; i < a.Length; i++)
        {
            difference |= a[i] ^ b[i];
        }

        return difference == 0;
    }
}
