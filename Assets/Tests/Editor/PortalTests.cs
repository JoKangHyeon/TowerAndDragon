using NUnit.Framework;
using UnityEngine;

public class PortalTests
{
    private GameObject _root;
    private Portal _portal;

    [SetUp]
    public void SetUp()
    {
        _root = new GameObject("PortalTests");
        _portal = _root.AddComponent<Portal>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_root);
    }

    // Seal()이 다음 ApplyCycle을 기다리지 않고 그 자리에서 바로 스폰을 멈추는지 확인한다.
    // (PortalProgressionController.ApplyCycle이 재실행될 경로가 없으므로 별도 방어 가드는 두지 않기로
    // 확인됐다 - Seal() 자신의 즉시 처리가 이 시나리오의 유일한 보장선이다.)
    [Test]
    public void Seal_SetsIsActiveFalseAndIsSealedTrueImmediately()
    {
        _portal.SetSpawnActive(true);
        Assert.That(_portal.IsActive, Is.True);

        _portal.Seal();

        Assert.That(_portal.IsSealed, Is.True);
        Assert.That(_portal.IsActive, Is.False);
    }
}
