using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using Object = UnityEngine.Object;

/// <summary>
/// 이어하기 복원이 정산 단계를 재생하지 않는다는 계약을 지킨다.
///
/// 저장 스냅샷은 언제나 "정산이 끝난 뒤" 상태다(SaveService 계약 2). 그래서 복원은
/// StartDay가 아니라 SeedRestoredDay + ResumeDay로 뒤 단계만 재생해야 한다.
/// ResumeDay가 OnDayStart(생산)나 OnDayStartUpkeep(소비)을 타게 되면 생산·유지비·알 성장·
/// 성 회복·이동권이 모두 이중 적용되어, 낮에 저장→로드를 반복하는 것만으로 자원이 무한 증식한다.
/// (2026-08-06 커밋 2a83b8f7에서 고친 버그다 - Docs/자원밸런싱_초안.md §6 참고.)
/// </summary>
public class CycleResumeContractTests
{
    private const int RESTORED_DAY_NUMBER = 5;
    private const string RUN_DATA_FIELD = "_currentRun";

    private GameObject _host;
    private CycleManager _cycleManager;

    private int _dayStartCount;
    private int _dayStartUpkeepCount;
    private int _dayReadyCount;

    [SetUp]
    public void SetUp()
    {
        _host = new GameObject(nameof(CycleResumeContractTests));

        var gameManager = _host.AddComponent<GameManager>();

        // RunData는 씬/프리팹 역직렬화로만 채워진다 - AddComponent로 만든 컴포넌트에서는
        // null이라 CurrentRun을 건드리는 순간 터진다. 테스트에서만 직접 주입한다.
        typeof(GameManager)
            .GetField(RUN_DATA_FIELD, BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(gameManager, new RunData());

        _cycleManager = _host.AddComponent<CycleManager>();
        _cycleManager.Construct(gameManager);

        // 코드로 만든 컴포넌트에는 씬에서 직렬화된 이벤트가 없으므로 직접 채운다.
        _cycleManager.OnDayStart = new UnityEvent<int>();
        _cycleManager.OnDayStartUpkeep = new UnityEvent<int>();
        _cycleManager.OnDayReady = new UnityEvent<int>();

        _dayStartCount = 0;
        _dayStartUpkeepCount = 0;
        _dayReadyCount = 0;

        _cycleManager.OnDayStart.AddListener(_ => _dayStartCount++);
        _cycleManager.OnDayStartUpkeep.AddListener(_ => _dayStartUpkeepCount++);
        _cycleManager.OnDayReady.AddListener(_ => _dayReadyCount++);
    }

    [TearDown]
    public void TearDown()
    {
        if (_host != null)
        {
            Object.DestroyImmediate(_host);
        }
    }

    [Test]
    public void ResumeDay_DoesNotReplaySettlementStages()
    {
        _cycleManager.SeedRestoredDay(RESTORED_DAY_NUMBER);
        _cycleManager.ResumeDay();

        Assert.That(_dayStartCount, Is.Zero, "이어하기가 생산 정산을 다시 돌리면 자원이 증식한다.");
        Assert.That(_dayStartUpkeepCount, Is.Zero, "이어하기가 소비 정산을 다시 돌리면 유지비가 두 번 걷힌다.");
        Assert.That(_dayReadyCount, Is.EqualTo(1), "이어하기도 낮 진입 연출·파생 상태는 재생해야 한다.");
    }

    [Test]
    public void SeedRestoredDay_KeepsSavedDayNumber()
    {
        _cycleManager.SeedRestoredDay(RESTORED_DAY_NUMBER);
        _cycleManager.ResumeDay();

        // 저장된 일차를 N-1로 시드한 뒤 StartDay로 올리는 방식이 아니어야 한다 - 그러면
        // 그 낮의 정산이 통째로 한 번 더 돈다.
        Assert.That(_cycleManager.CurrentDayNumber, Is.EqualTo(RESTORED_DAY_NUMBER));
        Assert.That(_cycleManager.CurrentCycle, Is.EqualTo(CycleManager.CycleState.Day));
    }

    // 대조군 - 새 낮은 정산 단계를 반드시 타야 한다. 위 두 테스트가 "아무 이벤트도 안 나가서"
    // 통과하는 상태로 썩는 것을 막는다.
    [Test]
    public void StartDay_RunsEverySettlementStage()
    {
        _cycleManager.StartDay();

        Assert.That(_dayStartCount, Is.EqualTo(1));
        Assert.That(_dayStartUpkeepCount, Is.EqualTo(1));
        Assert.That(_dayReadyCount, Is.EqualTo(1));
    }

    // 낮에 저장→로드를 여러 번 반복해도 정산이 한 번도 다시 돌지 않아야 한다(증식 회귀 방지).
    [Test]
    public void RepeatedResume_NeverReplaysSettlement()
    {
        const int RESUME_COUNT = 3;

        for (int i = 0; i < RESUME_COUNT; i++)
        {
            _cycleManager.SeedRestoredDay(RESTORED_DAY_NUMBER);
            _cycleManager.ResumeDay();
        }

        Assert.That(_dayStartCount, Is.Zero);
        Assert.That(_dayStartUpkeepCount, Is.Zero);
        Assert.That(_dayReadyCount, Is.EqualTo(RESUME_COUNT));
        Assert.That(_cycleManager.CurrentDayNumber, Is.EqualTo(RESTORED_DAY_NUMBER));
    }
}
