using NUnit.Framework;
using UnityEngine;

// SaveCrypto 봉투의 왕복·변조 감지 계약.
// 여기서 막고 싶은 것은 (1) 봉투를 씌웠다 벗기면 원문이 그대로 나오지 않는 경우와
// (2) 디스크의 봉투를 손으로 고쳤는데 조용히 통과하는 경우다.
public class SaveCryptoTests
{
    private const string SAMPLE_JSON =
        "{\"Meta\":{\"SchemaVersion\":2},\"Note\":\"한글·이모지 🐉 포함 UTF-8\",\"Amount\":123}";

    [Test]
    public void ProtectThenUnprotect_ReturnsOriginalText()
    {
        Assert.That(SaveCrypto.TryProtect(SAMPLE_JSON, out string payload, out string protectError), Is.True, protectError);
        Assert.That(SaveCrypto.TryUnprotect(payload, out string plainText, out string unprotectError), Is.True, unprotectError);
        Assert.That(plainText, Is.EqualTo(SAMPLE_JSON));
    }

    [Test]
    public void Protect_EmptyString_RoundTrips()
    {
        Assert.That(SaveCrypto.TryProtect(string.Empty, out string payload, out _), Is.True);
        Assert.That(SaveCrypto.TryUnprotect(payload, out string plainText, out _), Is.True);
        Assert.That(plainText, Is.EqualTo(string.Empty));
    }

    [Test]
    public void Protect_Null_FailsWithoutThrowing()
    {
        Assert.That(SaveCrypto.TryProtect(null, out string payload, out string error), Is.False);
        Assert.That(payload, Is.Null);
        Assert.That(error, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void Protect_SamePlainText_ProducesDifferentPayloadEachTime()
    {
        Assert.That(SaveCrypto.TryProtect(SAMPLE_JSON, out string first, out _), Is.True);
        Assert.That(SaveCrypto.TryProtect(SAMPLE_JSON, out string second, out _), Is.True);

        // IV가 매번 새로 뽑히므로 봉투가 달라야 한다. 그래도 둘 다 원문으로 풀려야 한다.
        Assert.That(second, Is.Not.EqualTo(first));
        Assert.That(SaveCrypto.TryUnprotect(first, out string a, out _), Is.True);
        Assert.That(SaveCrypto.TryUnprotect(second, out string b, out _), Is.True);
        Assert.That(a, Is.EqualTo(SAMPLE_JSON));
        Assert.That(b, Is.EqualTo(SAMPLE_JSON));
    }

    [Test]
    public void IsProtected_PlainJson_IsFalse()
    {
        Assert.That(SaveCrypto.IsProtected(SAMPLE_JSON), Is.False);
        Assert.That(SaveCrypto.IsProtected(null), Is.False);
        Assert.That(SaveCrypto.IsProtected(string.Empty), Is.False);
    }

    [Test]
    public void IsProtected_ActualPayload_IsTrue()
    {
        Assert.That(SaveCrypto.TryProtect(SAMPLE_JSON, out string payload, out _), Is.True);
        Assert.That(SaveCrypto.IsProtected(payload), Is.True);
    }

    [Test]
    public void Unprotect_TamperedCiphertext_FailsIntegrity()
    {
        Assert.That(SaveCrypto.TryProtect(SAMPLE_JSON, out string payload, out _), Is.True);

        string[] fields = payload.Split('.');
        // ciphertext 필드(인덱스 2)의 한 글자를 바꾼다. base64 알파벳 안에서 다른 문자로 치환.
        char[] cipher = fields[2].ToCharArray();
        int mid = cipher.Length / 2;
        cipher[mid] = cipher[mid] == 'A' ? 'B' : 'A';
        fields[2] = new string(cipher);
        string tampered = string.Join(".", fields);

        Assert.That(SaveCrypto.TryUnprotect(tampered, out string plainText, out string error), Is.False);
        Assert.That(plainText, Is.Null);
        Assert.That(error, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void Unprotect_TamperedHmac_Fails()
    {
        Assert.That(SaveCrypto.TryProtect(SAMPLE_JSON, out string payload, out _), Is.True);

        string[] fields = payload.Split('.');
        char[] mac = fields[3].ToCharArray();
        mac[0] = mac[0] == 'A' ? 'B' : 'A';
        fields[3] = new string(mac);

        Assert.That(SaveCrypto.TryUnprotect(string.Join(".", fields), out _, out _), Is.False);
    }

    [Test]
    public void Unprotect_WrongFieldCount_FailsWithoutThrowing()
    {
        Assert.That(SaveCrypto.TryUnprotect("TNDS1.only-two", out _, out string error), Is.False);
        Assert.That(error, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void Unprotect_GarbageBase64_FailsWithoutThrowing()
    {
        Assert.That(SaveCrypto.TryUnprotect("TNDS1.!!!.!!!.!!!", out _, out string error), Is.False);
        Assert.That(error, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void Unprotect_PlainTextWithoutMagic_Fails()
    {
        Assert.That(SaveCrypto.TryUnprotect(SAMPLE_JSON, out _, out string error), Is.False);
        Assert.That(error, Is.Not.Null.And.Not.Empty);
    }

    // SaveJson 직렬화 → 봉투 → 봉투 해제 → 역직렬화가 모든 필드를 보존하는지.
    [Test]
    public void DtoRoundTrip_ThroughEnvelope_PreservesEveryField()
    {
        var original = new BuildingPlacementDto
        {
            PrefabId = "Factory_SnowCrystal",
            Anchor = Vector3IntDto.From(new Vector3Int(-4, 7, 0)),
            RotationSteps = 2,
            AssignedPopulation = 5,
            ConstructedCycle = 3,
            BabyDragonIndex = 1,
        };

        Assert.That(SaveJson.TrySerialize(original, out string json, out string serializeError), Is.True, serializeError);
        Assert.That(SaveCrypto.TryProtect(json, out string payload, out string protectError), Is.True, protectError);
        Assert.That(SaveCrypto.TryUnprotect(payload, out string restoredJson, out string unprotectError), Is.True, unprotectError);
        Assert.That(
            SaveJson.TryDeserialize(restoredJson, out BuildingPlacementDto restored, out string deserializeError),
            Is.True,
            deserializeError);

        Assert.That(restored.PrefabId, Is.EqualTo(original.PrefabId));
        Assert.That(restored.Anchor.ToVector3Int(), Is.EqualTo(original.Anchor.ToVector3Int()));
        Assert.That(restored.RotationSteps, Is.EqualTo(original.RotationSteps));
        Assert.That(restored.AssignedPopulation, Is.EqualTo(original.AssignedPopulation));
        Assert.That(restored.ConstructedCycle, Is.EqualTo(original.ConstructedCycle));
        Assert.That(restored.BabyDragonIndex, Is.EqualTo(original.BabyDragonIndex));
    }
}
