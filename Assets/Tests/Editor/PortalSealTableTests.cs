using NUnit.Framework;
using UnityEngine;

public class PortalSealTableTests
{
    private PortalSealTable _table;

    [SetUp]
    public void SetUp()
    {
        _table = ScriptableObject.CreateInstance<PortalSealTable>();
    }

    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(_table);
    }

    [Test]
    public void SiteCount_NoSitesAuthored_ReturnsZero()
    {
        Assert.That(_table.SiteCount, Is.EqualTo(0));
    }

    [Test]
    public void SiteCount_MatchesAuthoredSiteArrayLength()
    {
        _table.SetSites(new[]
        {
            SiteEntryAt(PortalDirection.North, new Vector3Int(0, 0, 0)),
            SiteEntryAt(PortalDirection.South, new Vector3Int(1, 0, 0)),
        });

        Assert.That(_table.SiteCount, Is.EqualTo(2));
    }

    [Test]
    public void BuildSiteLookup_MapsOccupiedCoordsToPortalDirection()
    {
        Vector3Int anchor = new Vector3Int(5, 5, 0);
        _table.SetSites(new[] { SiteEntryAt(PortalDirection.East, anchor) });

        var lookup = new System.Collections.Generic.Dictionary<Vector3Int, PortalDirection>();
        _table.BuildSiteLookup(lookup);

        Assert.That(lookup[anchor], Is.EqualTo(PortalDirection.East));
    }

    // 1x1 풋프린트 하나만 차지하는 최소 봉인 영역 - 좌표 테스트에는 이걸로 충분하다.
    private static PortalSealTable.SiteEntry SiteEntryAt(PortalDirection direction, Vector3Int anchor) => new PortalSealTable.SiteEntry
    {
        PortalDirection = direction,
        AnchorCoord = anchor,
        Shape = new FootprintShape(new bool[1, 1] { { true } }),
    };
}
