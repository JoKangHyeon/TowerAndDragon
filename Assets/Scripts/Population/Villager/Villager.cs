using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>목적지에 도착한 뒤의 행동. 세 프로파일이 다른 지점은 이것뿐이라 이동·좌표해석·정렬은 전부 공유한다.</summary>
public enum VillagerProfile
{
    /// <summary>타워 - 도착해서 모션 한 번 하고 사라진다.</summary>
    TowerVisit,

    /// <summary>생산시설·연구소·랜드마크 - 계속 일한다. 소멸은 VillagerDispatchSystem이 지시한다.</summary>
    Resident,

    /// <summary>점령 원정 - 계속 일한다. 점령 완료 시 성으로 돌아간다.</summary>
    Expedition,

    /// <summary>목적지(성)에 도착하면 그대로 사라진다. 인구 회수와 원정 귀환에 쓴다.</summary>
    Recall
}

/// <summary>
/// 캐릭터의 겉모습 계열. 행동(<see cref="VillagerProfile"/>)과 일부러 분리했다 - 성으로 돌아가는
/// Recall은 행동이 하나뿐이지만 겉모습은 출발지에 따라 달라야 하기 때문이다
/// (타워에서 빠져나오면 병사, 원정에서 돌아오면 일꾼).
/// </summary>
public enum VillagerAppearance
{
    /// <summary>타워 배치·회수.</summary>
    Soldier,

    /// <summary>생산시설·연구소·랜드마크 배치·회수.</summary>
    Worker,

    /// <summary>점령 원정 발송·귀환.</summary>
    Expedition
}

/// <summary>캐릭터 한 명에게 내리는 지시. 필드가 늘어도 Dispatch 시그니처가 흔들리지 않도록 묶었다.</summary>
public readonly struct VillagerOrder
{
    public readonly VillagerProfile Profile;

    /// <summary>출발 셀. <see cref="HasOutboundLeg"/>가 false면 쓰이지 않는다.</summary>
    public readonly Vector3Int OriginCell;

    /// <summary>셀 대신 고정 월드 좌표에서 출발해야 할 때 쓴다. 성 출구처럼 시각 위치가 고정인 경우다.</summary>
    public readonly Vector3 OriginWorldPosition;

    /// <summary>도착해서 일할 셀. Recall 프로파일에서는 성이 된다.</summary>
    public readonly Vector3Int WorkCell;

    /// <summary>귀환 목적지. Expedition 프로파일에서만 쓴다.</summary>
    public readonly Vector3Int CastleCell;

    /// <summary>셀 대신 고정 월드 좌표로 귀환해야 할 때 쓴다.</summary>
    public readonly Vector3 CastleWorldPosition;

    /// <summary>같은 자리에 여럿이 겹치지 않도록 개체마다 다르게 주는 미세 오프셋.</summary>
    public readonly Vector3 SpreadOffset;

    /// <summary>false면 걸어오지 않고 작업 셀에서 곧바로 일하는 상태로 등장한다.
    /// 세이브 복원처럼 "이미 일하고 있던 상태"를 재현할 때 쓴다.</summary>
    public readonly bool HasOutboundLeg;

    public readonly bool HasFixedOriginWorldPosition;

    public readonly bool HasFixedCastleWorldPosition;

    public VillagerOrder(
        VillagerProfile profile,
        Vector3Int originCell,
        Vector3Int workCell,
        Vector3Int castleCell,
        Vector3 spreadOffset,
        bool hasOutboundLeg)
        : this(
            profile,
            originCell,
            workCell,
            castleCell,
            spreadOffset,
            hasOutboundLeg,
            false,
            default(Vector3),
            false,
            default(Vector3))
    {
    }

    public VillagerOrder(
        VillagerProfile profile,
        Vector3Int originCell,
        Vector3Int workCell,
        Vector3Int castleCell,
        Vector3 spreadOffset,
        bool hasOutboundLeg,
        bool hasFixedOriginWorldPosition,
        Vector3 originWorldPosition)
        : this(
            profile,
            originCell,
            workCell,
            castleCell,
            spreadOffset,
            hasOutboundLeg,
            hasFixedOriginWorldPosition,
            originWorldPosition,
            false,
            default(Vector3))
    {
    }

    public VillagerOrder(
        VillagerProfile profile,
        Vector3Int originCell,
        Vector3Int workCell,
        Vector3Int castleCell,
        Vector3 spreadOffset,
        bool hasOutboundLeg,
        bool hasFixedOriginWorldPosition,
        Vector3 originWorldPosition,
        bool hasFixedCastleWorldPosition,
        Vector3 castleWorldPosition)
    {
        Profile = profile;
        OriginCell = originCell;
        OriginWorldPosition = originWorldPosition;
        WorkCell = workCell;
        CastleCell = castleCell;
        CastleWorldPosition = castleWorldPosition;
        SpreadOffset = spreadOffset;
        HasOutboundLeg = hasOutboundLeg;
        HasFixedOriginWorldPosition = hasFixedOriginWorldPosition;
        HasFixedCastleWorldPosition = hasFixedCastleWorldPosition;
    }
}

/// <summary>
/// 인구 배치 연출 캐릭터 한 명의 수명. 이동 → 도착 모션 → (상주) → (귀환) → 소멸을
/// UniTask 한 줄기로 실행한다. 프로파일마다 선형 시나리오 하나뿐이라 상태 enum이나 전이표가 필요 없다.
///
/// 순수 장식이다 - 게임 규칙에 아무 영향을 주지 않고, 인구 배치는 이 캐릭터가 도착하기 전에 이미 끝나 있다.
/// </summary>
[RequireComponent(typeof(VillagerMovement))]
public sealed class Villager : MonoBehaviour
{
    private const string MOVE_ANIM_KEY = "Move";
    private const string WORK_ANIM_KEY = "Work";
    private const string CHEER_ANIM_KEY = "Cheer";

    private static readonly int MOVE_ANIM_HASH = Animator.StringToHash(MOVE_ANIM_KEY);
    private static readonly int WORK_ANIM_HASH = Animator.StringToHash(WORK_ANIM_KEY);
    private static readonly int CHEER_ANIM_HASH = Animator.StringToHash(CHEER_ANIM_KEY);

    // 상주 중인 캐릭터가 기다리는 지시. 취소 토큰 대신 플래그를 쓰는 이유:
    // 귀환은 "취소된 뒤에도 계속 이동해야 하는" 동작이라 토큰으로 표현하면 새 토큰을 다시 만들어야 한다.
    private enum VillagerCommand
    {
        None,
        Despawn,
        SendHome
    }

    /// <summary>소멸 직전에 알린다 - VillagerDispatchSystem이 활성 목록에서 지운다.</summary>
    public event Action<Villager> Finished;

    private VillagerMovement _movement;
    private Animator _animator;

    private VillagerOrder _order;
    private VillagerCommand _command = VillagerCommand.None;
    private float _cheerSeconds;

    private void Awake()
    {
        _movement = GetComponent<VillagerMovement>();

        // Worker 계열 프리팹은 스프라이트와 애니메이터를 자식(Graphics)에 둔다. GetComponentInChildren은
        // 자기 자신을 먼저 보므로 루트에 붙인 프리팹도 그대로 동작한다.
        // BaseMonster는 루트 GetComponent를 쓰는데, 그 차이를 놓치면 조용히 무동작한다.
        _animator = GetComponentInChildren<Animator>();
    }

    public void Construct(GridMap gridMap, float moveSpeed, float cheerSeconds)
    {
        _cheerSeconds = cheerSeconds;
        _movement.Construct(gridMap);
        _movement.SetSpeed(moveSpeed);
    }

    public void Dispatch(in VillagerOrder order)
    {
        _order = order;

        if (order.HasOutboundLeg && order.HasFixedOriginWorldPosition)
        {
            _movement.WarpWorld(order.OriginWorldPosition);
        }
        else
        {
            _movement.Warp(order.HasOutboundLeg ? order.OriginCell : order.WorkCell, order.SpreadOffset);
        }

        RunAsync(this.GetCancellationTokenOnDestroy()).Forget();
    }

    /// <summary>걸어가는 연출 없이 사라지게 한다. 밤 전환·건물 철거·인구 0 등에 쓴다.</summary>
    public void Despawn()
    {
        _command = VillagerCommand.Despawn;
    }

    /// <summary>성으로 걸어가 사라지게 한다. 점령 완료 후 귀환에 쓴다.</summary>
    public void SendHome()
    {
        // 이미 사라지기로 한 개체를 되돌리지 않는다 - 밤 전환과 점령 완료가 같은 프레임에 겹칠 수 있다.
        if (_command == VillagerCommand.None)
        {
            _command = VillagerCommand.SendHome;
        }
    }

    // 취소는 오브젝트 파괴 시에만 일어난다. UniTaskVoid + Forget은 OperationCanceledException을
    // 기본적으로 무시하므로(UniTaskScheduler 기본 설정) 별도 try/catch가 필요 없다 -
    // TutorialEndingSequencer.PlayAsync와 같은 형태다.
    private async UniTaskVoid RunAsync(CancellationToken token)
    {
        if (_order.HasOutboundLeg && !await WalkToWorkAsync(token))
        {
            Finish();
            return;
        }

        switch (_order.Profile)
        {
            case VillagerProfile.TowerVisit:
                PlayCheer();
                await UniTask.WaitForSeconds(_cheerSeconds, cancellationToken: token);
                break;

            case VillagerProfile.Recall:
                // 목적지가 곧 성이었다 - 도착했으므로 더 할 일이 없다.
                break;

            default:
                PlayWork();
                await UniTask.WaitUntil(() => _command != VillagerCommand.None, cancellationToken: token);

                if (_command == VillagerCommand.SendHome)
                {
                    await WalkHomeAsync(token);
                }

                break;
        }

        Finish();
    }

    private UniTask<bool> WalkToWorkAsync(CancellationToken token) =>
        _order.Profile == VillagerProfile.Recall && _order.HasFixedCastleWorldPosition
            ? WalkToWorldAsync(_order.CastleWorldPosition, token)
            : WalkToAsync(_order.WorkCell, token);

    private UniTask<bool> WalkHomeAsync(CancellationToken token) =>
        _order.HasFixedCastleWorldPosition
            ? WalkToWorldAsync(_order.CastleWorldPosition, token)
            : WalkToAsync(_order.CastleCell, token);

    // 목적지에 닿았으면 true. 도중에 Despawn 지시를 받아 중단했으면 false.
    // 이동 중에도 지시를 확인하는 이유: 밤이 시작되는 순간 걸어가던 캐릭터까지 즉시 사라져야 한다.
    private async UniTask<bool> WalkToAsync(Vector3Int cell, CancellationToken token)
    {
        PlayMove();
        _movement.SetDestination(cell, _order.SpreadOffset);
        _movement.Begin();

        await UniTask.WaitUntil(
            () => _movement.HasArrived || _command == VillagerCommand.Despawn,
            cancellationToken: token);

        _movement.Stop();
        PlayIdle();

        return _command != VillagerCommand.Despawn;
    }

    private async UniTask<bool> WalkToWorldAsync(Vector3 worldPosition, CancellationToken token)
    {
        PlayMove();
        _movement.SetDestinationWorld(worldPosition);
        _movement.Begin();

        await UniTask.WaitUntil(
            () => _movement.HasArrived || _command == VillagerCommand.Despawn,
            cancellationToken: token);

        _movement.Stop();
        PlayIdle();

        return _command != VillagerCommand.Despawn;
    }

    private void Finish()
    {
        Finished?.Invoke(this);
        Destroy(gameObject);
    }

    private void PlayMove() => SetLocomotionState(isMoving: true, isWorking: false);

    private void PlayIdle() => SetLocomotionState(isMoving: false, isWorking: false);

    private void PlayWork() => SetLocomotionState(isMoving: false, isWorking: true);

    private void PlayCheer()
    {
        SetLocomotionState(isMoving: false, isWorking: false);

        if (_animator != null)
        {
            _animator.SetTrigger(CHEER_ANIM_HASH);
        }
    }

    // 애니메이터가 없어도(프리팹에 아직 안 붙였거나 테스트용) 이동 자체는 동작해야 한다.
    private void SetLocomotionState(bool isMoving, bool isWorking)
    {
        if (_animator == null)
        {
            return;
        }

        _animator.SetBool(MOVE_ANIM_HASH, isMoving);
        _animator.SetBool(WORK_ANIM_HASH, isWorking);
    }
}
