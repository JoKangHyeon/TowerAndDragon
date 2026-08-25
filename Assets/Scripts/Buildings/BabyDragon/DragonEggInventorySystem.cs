using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 알(부화 전 새끼용)은 슬라임 소비 없이 매일 아침 day count만 증가시켜 성장시키고, 임계치 도달 시 부화시킨다.
/// 모든 알 획득 경로(시작 지급/디버그/추후 점령 보상)는 GrantEgg 하나만 호출하면 된다.
/// </summary>
public class DragonEggInventorySystem : MonoBehaviour
{
    [SerializeField] private GameManager _gameManager;
    [SerializeField] private CycleManager _cycleManager;
    [SerializeField] private BabyDragonDataCatalog _dataCatalog;

    [Header("시작 지급")]
    [Tooltip("게임 시작 시 자동으로 알을 지급할지 여부. 시작 알이 필요 없는 씬(디버그용 등)에서는 끈다.")]
    [SerializeField] private bool _grantStartingEgg;
    [Tooltip("시작 지급 알의 속성.")]
    [SerializeField] private DragonType _startingEggType = DragonType.Life;

    // 디버그 GUI가 알의 부화 진행도(며칠째/목표 며칠)를 표시할 때 카탈로그를 다시 참조로 안 받고 이걸 쓴다.
    public BabyDragonDataCatalog DataCatalog => _dataCatalog;

    // 중력 적응(heavy_gravity) 등 런 수정치. GameManager를 경유하므로 새 인스펙터 참조가 필요 없다.
    private RunModifierSnapshot Snapshot =>
        RunModifiers.SnapshotOf(_gameManager != null ? _gameManager.RunModifierService : null);

    /// <summary>런 수정치를 반영한 실제 부화 필요 일수. 표시하는 쪽이 부화 판정과 같은 값을 쓰도록
    /// 계산을 여기서만 한다 - UI가 BabyDragonData.DaysToHatch를 직접 읽으면 표시와 실제가 어긋난다.</summary>
    public int GetDaysToHatch(BabyDragonData data)
    {
        return DragonEggHatchRules.ResolveDaysToHatch(data, Snapshot);
    }

    /// <summary>부화까지 남은 일수. <see cref="GetDaysToHatch"/>와 같은 출처를 쓴다.</summary>
    public int GetRemainingDays(DragonEgg egg, BabyDragonData data)
    {
        return DragonEggHatchRules.ResolveRemainingDays(egg, data, Snapshot);
    }

    // 알을 새로 얻은/부화한 시점 - 보상·시작 지급·디버그 등 모든 경로가 GrantEgg를 거치므로
    // 여기에 붙이면 향후 점령·랜드마크 보상이 추가돼도 알림이 자동으로 따라온다.
    public UnityEvent<DragonType> OnEggGranted = new();
    public UnityEvent<DragonType> OnEggHatched = new();

    private void OnEnable()
    {
        if (_cycleManager != null)
        {
            _cycleManager.OnDayStart.AddListener(HandleDayStart);
        }
    }

    private void OnDisable()
    {
        if (_cycleManager != null)
        {
            _cycleManager.OnDayStart.RemoveListener(HandleDayStart);
        }
    }

    /// <summary>
    /// 하루의 시작. 알을 하루치 성장시키고, 새 런의 첫날이면 시작 알을 지급한다.
    ///
    /// 시작 지급이 Start가 아니라 이 신호에 붙어 있는 이유: OnDayStart는 StartDay에서만 발화하고
    /// StartDay는 새 런(GameManager.StartNewRun)에서만 불린다. 이어하기는 SeedRestoredDay +
    /// ResumeDay로 들어와 OnDayStart를 발화하지 않으므로 여기로 오지 않는다 - Start에 두면
    /// 불러오기로 씬을 리로드할 때마다 알이 다시 지급되고 획득 알림이 다시 뜬다.
    /// 로드 실패 폴백도 StartNewRun을 거치므로 그 경로에서는 정상 지급된다.
    /// </summary>
    private void HandleDayStart(int dayNumber)
    {
        GrowAll(dayNumber);

        if (_grantStartingEgg && dayNumber == CycleManager.FIRST_DAY_NUMBER)
        {
            GrantStartingEggAsync().Forget();
        }
    }

    // CLAUDE.md 이벤트 규칙: 다른 오브젝트(토스트 등)의 구독이 Start까지 끝난 뒤 발화되도록 한 프레임 미룬다.
    private async UniTaskVoid GrantStartingEggAsync()
    {
        await UniTask.Yield(this.GetCancellationTokenOnDestroy());
        GrantEgg(_startingEggType);
    }

    public bool GrantEgg(DragonType dragonType) => GrantEgg(dragonType, 0);

    /// <summary>
    /// 이미 며칠 자란 알을 지급한다. 튜토리얼처럼 "다음 날 아침에 부화"를 보여줘야 하는 경우에 쓴다 -
    /// 부화까지의 날수는 BabyDragonData가 정하므로, 그 값을 건드리지 않고 출발점만 앞당긴다.
    /// </summary>
    public bool GrantEgg(DragonType dragonType, int initialFedDayCount)
    {
        if (_gameManager == null || _gameManager.CurrentRun == null)
        {
            return false;
        }

        _gameManager.CurrentRun.DragonEggs.Add(new DragonEgg
        {
            DragonType = dragonType,
            FedDayCount = Mathf.Max(0, initialFedDayCount),
        });

        _gameManager.CurrentRun.OnInventoryChanged.Invoke();
        OnEggGranted?.Invoke(dragonType);
        return true;
    }

    // 일차 인자는 쓰지 않는다 - 부화 속도는 날짜와 무관하다.
    private void GrowAll(int _)
    {
        if (_gameManager == null || _dataCatalog == null)
        {
            return;
        }

        // 부화로 원본 리스트가 변경되므로 스냅샷을 순회한다.
        DragonEgg[] snapshot = _gameManager.CurrentRun.DragonEggs.ToArray();

        bool changed = false;
        foreach (DragonEgg egg in snapshot)
        {
            changed |= Grow(egg);
        }

        // 하루치 성장을 다 처리한 뒤 한 번만 알린다.
        // 부화뿐 아니라 '진행도만 오른' 경우도 알림 대상이다 - UI의 부화 진행도 표시가 갱신되어야 한다.
        // 알마다 발화하면 알이 여러 개일 때 같은 날에 UI가 여러 번 재구성된다.
        if (changed)
        {
            _gameManager.CurrentRun.OnInventoryChanged.Invoke();
        }
    }

    // 이 알이 실제로 변경(진행도 증가 또는 부화)되었으면 true - 호출자가 알림 발행 여부를 정한다.
    private bool Grow(DragonEgg egg)
    {
        if (!_dataCatalog.TryResolve(egg.DragonType, out BabyDragonData data))
        {
            Debug.LogError($"[DragonEggInventorySystem] 속성 {egg.DragonType}에 대응하는 BabyDragonData가 카탈로그에 없습니다.");
            return false;
        }

        // 비교가 두 곳이라 유효 일수는 반드시 같은 함수로 구한다 - 한쪽만 런 수정치를 반영하면
        // 그날 부화해야 할 알이 하루 더 남거나 그 반대가 된다.
        RunModifierSnapshot snapshot = Snapshot;

        // 초기 지급 등으로 FedDayCount가 이미 목표치를 채운 알은 바로 부화시킨다.
        if (DragonEggHatchRules.IsReadyToHatch(egg, data, snapshot))
        {
            Hatch(egg);
            return true;
        }

        egg.FedDayCount += 1;

        if (!DragonEggHatchRules.IsReadyToHatch(egg, data, snapshot))
        {
            // 진행도만 올랐다 - 부화는 아니지만 남은 일수 표시가 바뀌므로 변경으로 취급한다.
            return true;
        }

        Hatch(egg);
        return true;
    }

    // 알림은 호출자(GrowAll)가 하루치를 모두 처리한 뒤 한 번만 발행한다 - 여기서 발화하지 않는다.
    private void Hatch(DragonEgg egg)
    {
        _gameManager.CurrentRun.DragonEggs.Remove(egg);
        _gameManager.CurrentRun.BabyDragons.Add(new BabyDragon
        {
            DragonType = egg.DragonType,
            IsInTower = false,
        });
        _gameManager.CurrentRun.OnInventoryChanged.Invoke();
        OnEggHatched?.Invoke(egg.DragonType);

        Debug.Log($"[DragonEggInventorySystem] 알(속성 {egg.DragonType})이 부화했습니다.");
    }
}
