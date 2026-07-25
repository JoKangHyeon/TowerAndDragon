using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class PortalProgressionControllerTests
{
    private GameObject _root;
    private PortalProgressionController _controller;
    private Dictionary<PortalDirection, Portal> _portals;

    [SetUp]
    public void SetUp()
    {
        _root = new GameObject("PortalProgressionControllerTests");
        _root.SetActive(false);
        _controller = _root.AddComponent<PortalProgressionController>();
        _portals = new Dictionary<PortalDirection, Portal>();

        List<Portal> portalList = new List<Portal>
        {
            CreatePortal(PortalDirection.North),
            CreatePortal(PortalDirection.South),
            CreatePortal(PortalDirection.East),
            CreatePortal(PortalDirection.West),
        };

        SetPrivateField(_controller, "_portals", portalList);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_root);
    }

    [TestCase(1, true, false, false, false)]
    [TestCase(2, true, true, false, false)]
    [TestCase(3, true, true, true, false)]
    [TestCase(4, true, true, true, true)]
    public void ApplyCycle_ValidCycle_ActivatesCumulativePortals(
        int cycleNumber,
        bool northActive,
        bool southActive,
        bool eastActive,
        bool westActive)
    {
        _controller.ApplyCycle(cycleNumber);

        Assert.That(_portals[PortalDirection.North].IsActive, Is.EqualTo(northActive));
        Assert.That(_portals[PortalDirection.South].IsActive, Is.EqualTo(southActive));
        Assert.That(_portals[PortalDirection.East].IsActive, Is.EqualTo(eastActive));
        Assert.That(_portals[PortalDirection.West].IsActive, Is.EqualTo(westActive));
    }

    [Test]
    public void ApplyCycle_SameCycleRepeated_KeepsSamePortalState()
    {
        _controller.ApplyCycle(2);
        _controller.ApplyCycle(2);

        Assert.That(_portals[PortalDirection.North].IsActive, Is.True);
        Assert.That(_portals[PortalDirection.South].IsActive, Is.True);
        Assert.That(_portals[PortalDirection.East].IsActive, Is.False);
        Assert.That(_portals[PortalDirection.West].IsActive, Is.False);
    }

    private Portal CreatePortal(PortalDirection portalDirection)
    {
        GameObject portalObject = new GameObject(portalDirection.ToString());
        portalObject.transform.SetParent(_root.transform);
        Portal portal = portalObject.AddComponent<Portal>();
        SetPrivateField(portal, "_portalDirectionId", portalDirection);
        _portals.Add(portalDirection, portal);
        return portal;
    }

    private static void SetPrivateField<TTarget, TValue>(
        TTarget target,
        string fieldName,
        TValue value)
    {
        FieldInfo field = typeof(TTarget).GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.That(field, Is.Not.Null);
        field.SetValue(target, value);
    }
}
