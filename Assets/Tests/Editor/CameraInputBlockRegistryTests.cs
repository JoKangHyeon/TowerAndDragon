using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// UI_CameraInputBlocker 자체가 아니라 임시 GameObject의 RectTransform을 대역으로 쓴다 -
// EditMode에서 AddComponent<UI_CameraInputBlocker>는 OnEnable을 태우지 않아 아무것도 검증하지 못한다.
// 레지스트리는 Register/Unregister에 넘어온 Component가 무엇인지 신경 쓰지 않으므로 대역으로 충분하다.
public class CameraInputBlockRegistryTests
{
    private readonly List<GameObject> _spawnedObjects = new();

    [TearDown]
    public void TearDown()
    {
        // static 레지스트리라 EditMode 테스트끼리 상태를 공유한다 - 다음 테스트가 시작하기 전에 비운다.
        foreach (GameObject spawned in _spawnedObjects)
        {
            if (spawned != null)
            {
                CameraInputBlockRegistry.Unregister(spawned.GetComponent<Transform>());
                Object.DestroyImmediate(spawned);
            }
        }

        _spawnedObjects.Clear();
    }

    [Test]
    public void IsBlocked_WhenEmpty_ReturnsFalse()
    {
        Assert.That(CameraInputBlockRegistry.IsBlocked, Is.False);
    }

    [Test]
    public void Register_SingleBlocker_ReportsBlocked()
    {
        Component blocker = CreateBlockerStandIn();

        CameraInputBlockRegistry.Register(blocker);

        Assert.That(CameraInputBlockRegistry.IsBlocked, Is.True);
    }

    [Test]
    public void Unregister_LastBlocker_ReportsUnblocked()
    {
        Component blocker = CreateBlockerStandIn();
        CameraInputBlockRegistry.Register(blocker);

        CameraInputBlockRegistry.Unregister(blocker);

        Assert.That(CameraInputBlockRegistry.IsBlocked, Is.False);
    }

    [Test]
    public void Register_SameBlockerTwice_DoesNotDoubleCount()
    {
        Component blocker = CreateBlockerStandIn();

        CameraInputBlockRegistry.Register(blocker);
        CameraInputBlockRegistry.Register(blocker);
        CameraInputBlockRegistry.Unregister(blocker);

        // 카운터가 아니라 대상 목록이므로, 두 번 등록해도 한 번의 해제로 완전히 빠져야 한다.
        Assert.That(CameraInputBlockRegistry.IsBlocked, Is.False);
    }

    [Test]
    public void Unregister_OneOfTwoBlockers_StillReportsBlocked()
    {
        Component first  = CreateBlockerStandIn();
        Component second = CreateBlockerStandIn();
        CameraInputBlockRegistry.Register(first);
        CameraInputBlockRegistry.Register(second);

        CameraInputBlockRegistry.Unregister(first);

        Assert.That(CameraInputBlockRegistry.IsBlocked, Is.True);
    }

    [Test]
    public void IsBlocked_AfterRegisteredObjectDestroyed_SelfRecovers()
    {
        GameObject spawned = new GameObject("CameraInputBlockRegistryTests_Destroyed");
        Component blocker = spawned.GetComponent<Transform>();
        CameraInputBlockRegistry.Register(blocker);

        Object.DestroyImmediate(spawned);

        // 짝이 어긋나 Unregister가 한 번도 안 불려도, 파괴된 항목은 다음 조회에서 스스로 걸러져야 한다 -
        // 그러지 않으면 카메라 조작이 영영 막힌 채로 남는다.
        Assert.That(CameraInputBlockRegistry.IsBlocked, Is.False);
    }

    [Test]
    public void Unregister_NeverRegistered_DoesNotThrowOrAffectState()
    {
        Component neverRegistered = CreateBlockerStandIn();

        Assert.DoesNotThrow(() => CameraInputBlockRegistry.Unregister(neverRegistered));
        Assert.That(CameraInputBlockRegistry.IsBlocked, Is.False);
    }

    private Component CreateBlockerStandIn()
    {
        GameObject spawned = new GameObject("CameraInputBlockRegistryTests_Blocker");
        _spawnedObjects.Add(spawned);
        return spawned.GetComponent<Transform>();
    }
}
