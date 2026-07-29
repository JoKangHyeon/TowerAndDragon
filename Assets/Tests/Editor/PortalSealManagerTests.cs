using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// PortalSealManager.Construct()가 씬 배선(Awake)과 테스트가 공유하는 공개 진입점이라 리플렉션이 필요 없다.
// HandleBuildingAdded(GridMap 이벤트 콜백, private)는 "건물→좌표→포탈 방향" 해석만 담당하고 GridMap 의존이
// 없는 핵심 로직(RegisterStoneBuilt, public)에 위임하므로, 이 로직은 GridMap 없이 직접 검증할 수 있다.
public class PortalSealManagerTests
{
    private GameObject _managerObject;
    private PortalSealManager _manager;
    private GameObject _gameManagerObject;
    private GameManager _gameManager;
    private PortalSealTable _table;
    private List<Portal> _portals;
    private readonly List<GameObject> _spawnedObjects = new();

    [SetUp]
    public void SetUp()
    {
        _managerObject = new GameObject("PortalSealManagerTests");
        _manager = _managerObject.AddComponent<PortalSealManager>();

        // GameManager.Awake()가 참조 없는 _cycleManager.Construct(this)를 호출하므로,
        // 비활성 상태로 AddComponent해 Awake 실행 자체를 건너뛴다(리플렉션이 아니라 표준 Unity API).
        _gameManagerObject = new GameObject("GameManager");
        _gameManagerObject.SetActive(false);
        _gameManager = _gameManagerObject.AddComponent<GameManager>();

        _table = ScriptableObject.CreateInstance<PortalSealTable>();
        _table.SetSites(new[]
        {
            SiteEntryAt(PortalDirection.North, new Vector3Int(0, 0, 0)),
            SiteEntryAt(PortalDirection.South, new Vector3Int(1, 0, 0)),
            SiteEntryAt(PortalDirection.East, new Vector3Int(2, 0, 0)),
            SiteEntryAt(PortalDirection.West, new Vector3Int(3, 0, 0)),
        });

        // 이 매니저의 로직은 포탈의 PortalDirectionId를 읽지 않는다(_portals 리스트 전체를 무조건 Seal()) -
        // 그래서 방향을 지정할 필요 없이 평범한 Portal 4개만 있으면 된다.
        _portals = new List<Portal>();
        for (int i = 0; i < 4; i++)
            _portals.Add(CreatePortal($"Portal_{i}"));

        _manager.Construct(gridMap: null, _gameManager, _table, _portals);
    }

    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(_managerObject);
        UnityEngine.Object.DestroyImmediate(_gameManagerObject);
        UnityEngine.Object.DestroyImmediate(_table);

        foreach (GameObject spawned in _spawnedObjects)
            UnityEngine.Object.DestroyImmediate(spawned);
        _spawnedObjects.Clear();
    }

    [Test]
    public void RegisterStoneBuilt_ThreeOfFourSites_NoneSealedYet()
    {
        _manager.RegisterStoneBuilt(PortalDirection.North);
        _manager.RegisterStoneBuilt(PortalDirection.South);
        _manager.RegisterStoneBuilt(PortalDirection.East);

        foreach (Portal portal in _portals)
            Assert.That(portal.IsSealed, Is.False);

        Assert.That(_gameManager.CurrentGameResult, Is.EqualTo(GameManager.GameResult.None));
        Assert.That(_manager.BuiltCount, Is.EqualTo(3));
    }

    [Test]
    public void RegisterStoneBuilt_FourthSite_SealsAllPortalsAndTriggersVictory()
    {
        _manager.RegisterStoneBuilt(PortalDirection.North);
        _manager.RegisterStoneBuilt(PortalDirection.South);
        _manager.RegisterStoneBuilt(PortalDirection.East);
        _manager.RegisterStoneBuilt(PortalDirection.West);

        foreach (Portal portal in _portals)
        {
            Assert.That(portal.IsSealed, Is.True);
            Assert.That(portal.IsActive, Is.False);
        }

        Assert.That(_gameManager.CurrentGameResult, Is.EqualTo(GameManager.GameResult.Victory));
    }

    // 같은 포탈이 두 번 들어와도(이론상 배치 게이트가 막지만) 카운트가 중복 증가하지 않는지 -
    // HashSet 기반이므로 Add()가 중복을 자연히 걸러낸다.
    [Test]
    public void RegisterStoneBuilt_SameDirectionTwice_CountsOnce()
    {
        _manager.RegisterStoneBuilt(PortalDirection.North);
        _manager.RegisterStoneBuilt(PortalDirection.North);

        Assert.That(_manager.BuiltCount, Is.EqualTo(1));
    }

    [Test]
    public void TryResolveOpenSite_FootprintInsideUnbuiltSite_ReturnsTrue()
    {
        bool result = _manager.TryResolveOpenSite(
            new List<Vector3Int> { new Vector3Int(0, 0, 0) },
            out PortalDirection direction);

        Assert.That(result, Is.True);
        Assert.That(direction, Is.EqualTo(PortalDirection.North));
    }

    [Test]
    public void TryResolveOpenSite_FootprintOutsideAnySite_ReturnsFalse()
    {
        bool result = _manager.TryResolveOpenSite(
            new List<Vector3Int> { new Vector3Int(999, 999, 0) },
            out _);

        Assert.That(result, Is.False);
    }

    [Test]
    public void TryResolveOpenSite_FootprintAcrossTwoSites_ReturnsFalse()
    {
        bool result = _manager.TryResolveOpenSite(
            new List<Vector3Int> { new Vector3Int(0, 0, 0), new Vector3Int(1, 0, 0) },
            out _);

        Assert.That(result, Is.False);
    }

    [Test]
    public void TryResolveOpenSite_SiteAlreadyBuilt_ReturnsFalse()
    {
        _manager.RegisterStoneBuilt(PortalDirection.North);

        bool result = _manager.TryResolveOpenSite(
            new List<Vector3Int> { new Vector3Int(0, 0, 0) },
            out _);

        Assert.That(result, Is.False);
    }

    [Test]
    public void IsUnlocked_NoUnlockQueryAssigned_ReturnsTrueForAnyOrder()
    {
        Assert.That(_manager.IsUnlocked(1), Is.True);
        Assert.That(_manager.IsUnlocked(4), Is.True);
    }

    // 봉인석은 1~4차 순서로만 해금되므로, 낮은 차수만 풀리고 높은 차수는 아직 안 풀린 상태를 검증한다.
    [Test]
    public void IsUnlocked_UnlockQueryStubUnlocksUpToOrderTwo_ReturnsTrueOnlyUpToThatOrder()
    {
        _manager.UnlockQuery = new StubUnlockQuery(unlockedUpToOrder: 2);

        Assert.That(_manager.IsUnlocked(1), Is.True);
        Assert.That(_manager.IsUnlocked(2), Is.True);
        Assert.That(_manager.IsUnlocked(3), Is.False);
        Assert.That(_manager.IsUnlocked(4), Is.False);
    }

    private Portal CreatePortal(string name)
    {
        GameObject portalObject = new GameObject(name);
        _spawnedObjects.Add(portalObject);
        return portalObject.AddComponent<Portal>();
    }

    private static PortalSealTable.SiteEntry SiteEntryAt(PortalDirection direction, Vector3Int anchor) => new PortalSealTable.SiteEntry
    {
        PortalDirection = direction,
        AnchorCoord = anchor,
        Shape = new FootprintShape(new bool[1, 1] { { true } }),
    };

    private sealed class StubUnlockQuery : ISealStoneUnlockQuery
    {
        private readonly int _unlockedUpToOrder;
        public StubUnlockQuery(int unlockedUpToOrder) => _unlockedUpToOrder = unlockedUpToOrder;
        public bool IsUnlocked(int order) => order <= _unlockedUpToOrder;
    }
}
