using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

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
    private const int BASE_LAYER_INDEX = 0;
    private const string BASE_LAYER_NAME = "Base Layer";
    private const string IDLE_ANIM_STATE = "Idle";
    // 컨트롤러마다 가진 파라미터가 조금씩 다르므로 실제 호출은 HasParameter 가드로 걸러낸다.
    private const string WORK_ANIM_KEY = "Work";
    private const string CHEER_ANIM_KEY = "Cheer";
    private const string IDLE_ANIM_STATE_PATH = BASE_LAYER_NAME + "." + IDLE_ANIM_STATE;
    private const string MOVE_ANIM_STATE_PATH = BASE_LAYER_NAME + "." + Defines.ANIM_MOVE;
    private const string WORK_ANIM_STATE_PATH = BASE_LAYER_NAME + "." + WORK_ANIM_KEY;
#if UNITY_EDITOR
    private const string CONTROLLER_ASSET_DIRECTORY = "Assets/Prefabs/Villager/Animator/";
    private const string CONTROLLER_ASSET_EXTENSION = ".controller";
    private const string CLONE_NAME_SUFFIX = "(Clone)";
#endif

    private static readonly int MOVE_ANIM_HASH = Animator.StringToHash(Defines.ANIM_MOVE);
    private static readonly int ATTACK_ANIM_HASH = Animator.StringToHash(Defines.ANIM_ENEMY_ATTACK);
    private static readonly int WORK_ANIM_HASH = Animator.StringToHash(WORK_ANIM_KEY);
    private static readonly int CHEER_ANIM_HASH = Animator.StringToHash(CHEER_ANIM_KEY);
    private static readonly int IDLE_STATE_HASH = Animator.StringToHash(IDLE_ANIM_STATE_PATH);
    private static readonly int MOVE_STATE_HASH = Animator.StringToHash(MOVE_ANIM_STATE_PATH);
    private static readonly int WORK_STATE_HASH = Animator.StringToHash(WORK_ANIM_STATE_PATH);
    private static readonly int IDLE_SHORT_STATE_HASH = Animator.StringToHash(IDLE_ANIM_STATE);
    private static readonly int MOVE_SHORT_STATE_HASH = Animator.StringToHash(Defines.ANIM_MOVE);
    private static readonly int WORK_SHORT_STATE_HASH = Animator.StringToHash(WORK_ANIM_KEY);

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

    [SerializeField] private RuntimeAnimatorController _fallbackAnimatorController;

    private VillagerMovement _movement;
    private Animator _animator;
    private SpriteRenderer[] _renderers;
    private readonly HashSet<int> _animatorParameterHashes = new();
    private RuntimeAnimatorController _cachedAnimatorController;

    private VillagerOrder _order;
    private VillagerCommand _command = VillagerCommand.None;
    private float _cheerSeconds;
    private float _fadeOutSeconds;
    private float _attackIntervalSeconds;
    private bool _hasFinished;
    private bool _hasCachedParameters;
    private bool _wantsMove;
    private bool _wantsWork;
    private bool _hasDesiredState;
    private bool _hasAppliedState;
    private int _desiredStateHash;
    private int _desiredShortStateHash;
    private int _appliedStateHash;

    private void Awake()
    {
        _movement = GetComponent<VillagerMovement>();

        // Worker 계열 프리팹은 스프라이트와 애니메이터를 자식(Graphics)에 둔다. GetComponentInChildren은
        // 자기 자신을 먼저 보므로 루트에 붙인 프리팹도 그대로 동작한다.
        // BaseMonster는 루트 GetComponent를 쓰는데, 그 차이를 놓치면 조용히 무동작한다.
        _animator = GetComponentInChildren<Animator>();
        EnsureAnimatorController();

        // 사라질 때 같이 흐려져야 하므로 그림자 등 자식 스프라이트까지 전부 잡는다
        // (IsometricDepthSorter가 정렬에 쓰는 것과 같은 집합. 저쪽은 sortingOrder만 건드려 충돌하지 않는다).
        _renderers = GetComponentsInChildren<SpriteRenderer>(true);
    }

    public void Construct(
        GridMap gridMap,
        float moveSpeed,
        float cheerSeconds,
        float fadeOutSeconds,
        float attackIntervalSeconds)
    {
        _cheerSeconds = cheerSeconds;
        _fadeOutSeconds = fadeOutSeconds;
        _attackIntervalSeconds = attackIntervalSeconds;
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
        // Instantiate와 같은 프레임에는 자식 Animator가 아직 초기화되지 않아 파라미터를 읽을 수 없다.
        // 위치는 Dispatch의 Warp에서 이미 잡아뒀으므로 한 프레임 미뤄도 눈에 띄지 않는다.
        await UniTask.Yield(PlayerLoopTiming.Update, token);

        if (_order.HasOutboundLeg && !await WalkToWorkAsync(token))
        {
            await FinishAsync(token);
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
                // 상주(생산시설·연구소·랜드마크)와 원정 크루 모두 "일하는 중"을 보여줘야 한다.
                // 컨트롤러에 계속 유지되는 Work 상태가 있으면 그걸 쓰고(Villager_WorkerDig 같은 전용
                // 컨트롤러), 없으면 PixelWorld 원본에 있는 EnemyAttack 트리거를 반복해 흉내낸다.
                // 덕분에 프리팹마다 컨트롤러가 달라도 같은 코드로 굴러간다.
                if (HasParameter(WORK_ANIM_HASH))
                {
                    PlayWork();
                    await UniTask.WaitUntil(() => _command != VillagerCommand.None, cancellationToken: token);
                }
                else
                {
                    await AttackUntilCommandedAsync(token);
                }

                if (_command == VillagerCommand.SendHome)
                {
                    await WalkHomeAsync(token);
                }

                break;
        }

        await FinishAsync(token);
    }

    private UniTask<bool> WalkToWorkAsync(CancellationToken token) =>
        _order.Profile == VillagerProfile.Recall && _order.HasFixedCastleWorldPosition
            ? WalkToWorldAsync(_order.CastleWorldPosition, token)
            : WalkToAsync(_order.WorkCell, token);

    private UniTask<bool> WalkHomeAsync(CancellationToken token) =>
        _order.HasFixedCastleWorldPosition
            ? WalkToWorldAsync(_order.CastleWorldPosition, token)
            : WalkToAsync(_order.CastleCell, token);

    // 자리에 선 캐릭터(상주·원정 크루)가 소멸/귀환 지시를 받을 때까지 계속 모션을 반복한다.
    // EnemyAttack은 Bool이 아니라 Trigger이고 그 상태는 한 번 재생하면 Idle로 빠지므로
    // (PixelWorld 컨트롤러의 m_HasExitTime: 1), 이어지는 그림을 만들려면 주기적으로 다시 쏴야 한다.
    // Work 상태를 가진 컨트롤러에서는 이 경로를 타지 않는다.
    private async UniTask AttackUntilCommandedAsync(CancellationToken token)
    {
        SetLocomotionState(isMoving: false, isWorking: false);

        while (_command == VillagerCommand.None)
        {
            SetTrigger(ATTACK_ANIM_HASH);

            // 대기 중에도 지시를 확인한다 - 그냥 기다리면 점령이 끝나고도 한 박자 늦게 출발한다.
            float elapsed = 0f;

            while (elapsed < _attackIntervalSeconds && _command == VillagerCommand.None)
            {
                elapsed += Time.deltaTime;
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
        }
    }

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

    // 사라지기 전에 서서히 투명해진다. 모든 소멸 경로(타워 방문 종료·회수 도착·상주 해제·밤 정리)가
    // 이 한 곳을 지나므로 어느 경우든 툭 사라지지 않는다.
    private async UniTask FinishAsync(CancellationToken token)
    {
        await FadeOutAsync(token);
        Finish();
    }

    private async UniTask FadeOutAsync(CancellationToken token)
    {
        if (_fadeOutSeconds <= 0f || _renderers.Length == 0)
        {
            return;
        }

        float elapsed = 0f;

        // Time.deltaTime을 쓰므로 일시정지·배속(GameSpeedManager)에 이동과 똑같이 따라간다.
        while (elapsed < _fadeOutSeconds)
        {
            elapsed += Time.deltaTime;
            SetAlpha(1f - Mathf.Clamp01(elapsed / _fadeOutSeconds));

            await UniTask.Yield(PlayerLoopTiming.Update, token);
        }
    }

    private void SetAlpha(float alpha)
    {
        foreach (SpriteRenderer spriteRenderer in _renderers)
        {
            if (spriteRenderer == null)
            {
                continue;
            }

            Color color = spriteRenderer.color;
            color.a = alpha;
            spriteRenderer.color = color;
        }
    }

    private void Finish()
    {
        if (_hasFinished)
        {
            return;
        }

        _hasFinished = true;
        Finished?.Invoke(this);
        Destroy(gameObject);
    }

    // 페이드 도중에 밖에서 파괴되면(씬 언로드 등) Finish가 돌지 못해 디스패치의 활성 목록에
    // 죽은 항목이 남는다 - 그 경우에도 반드시 알린다.
    private void OnDestroy()
    {
        if (_hasFinished)
        {
            return;
        }

        _hasFinished = true;
        Finished?.Invoke(this);
    }

    private void PlayMove() => SetLocomotionState(isMoving: true, isWorking: false);

    private void PlayIdle() => SetLocomotionState(isMoving: false, isWorking: false);

    private void PlayWork() => SetLocomotionState(isMoving: false, isWorking: true);

    private void PlayCheer()
    {
        SetLocomotionState(isMoving: false, isWorking: false);
        SetTrigger(CHEER_ANIM_HASH);
    }

    // 원하는 상태를 기록만 하고, 실제 반영은 Update가 매 프레임 한다.
    // 여기서 바로 SetBool을 부르면 애니메이터 초기화 타이밍에 걸린다 - 자식 Animator는 루트보다
    // 늦게 준비될 수 있는데, 상태 전환은 걷기 시작할 때 "한 번"만 일어나므로 그 한 번을 놓치면
    // 걷는 내내 애니메이션이 죽는다. 매 프레임 다시 넣으면 언제 준비되든 반드시 반영된다.
    private void SetLocomotionState(bool isMoving, bool isWorking)
    {
        _wantsMove = isMoving;
        _wantsWork = isWorking;
        _desiredStateHash = ResolveStateHash(isMoving, isWorking);
        _desiredShortStateHash = ResolveShortStateHash(isMoving, isWorking);
        _hasDesiredState = true;

        if (!_hasAppliedState || _appliedStateHash != _desiredStateHash)
        {
            _hasAppliedState = false;
        }
    }

    private void Update()
    {
        if (_animator == null)
        {
            return;
        }

        EnsureAnimatorController();

        SetBool(MOVE_ANIM_HASH, _wantsMove);
        SetBool(WORK_ANIM_HASH, _wantsWork);
        ApplyDesiredState();
    }

    // 컨트롤러마다 있는 파라미터가 다르다(PixelWorld 원본에는 Move/EnemyAttack만 있다).
    // 없는 파라미터에 값을 넣으면 Unity가 매 호출마다 경고를 뱉으므로 미리 걸러낸다.
    private void SetBool(int hash, bool value)
    {
        if (HasParameter(hash))
        {
            _animator.SetBool(hash, value);
        }
    }

    private void SetTrigger(int hash)
    {
        if (HasParameter(hash))
        {
            _animator.SetTrigger(hash);
        }
    }

    private bool HasParameter(int hash)
    {
        if (_animator == null)
        {
            return false;
        }

        EnsureAnimatorController();
        EnsureParameterCache();
        return _animatorParameterHashes.Contains(hash);
    }

    private static int ResolveStateHash(bool isMoving, bool isWorking)
    {
        if (isMoving)
        {
            return MOVE_STATE_HASH;
        }

        return isWorking ? WORK_STATE_HASH : IDLE_STATE_HASH;
    }

    private static int ResolveShortStateHash(bool isMoving, bool isWorking)
    {
        if (isMoving)
        {
            return MOVE_SHORT_STATE_HASH;
        }

        return isWorking ? WORK_SHORT_STATE_HASH : IDLE_SHORT_STATE_HASH;
    }

    private void ApplyDesiredState()
    {
        EnsureAnimatorController();

        if (!_hasDesiredState ||
            _hasAppliedState ||
            !_animator.isInitialized ||
            _animator.runtimeAnimatorController == null)
        {
            return;
        }

        if (!TryPlayState(_desiredStateHash) && !TryPlayState(_desiredShortStateHash))
        {
            return;
        }

        _appliedStateHash = _desiredStateHash;
        _hasAppliedState = true;
    }

    private bool TryPlayState(int stateHash)
    {
        if (!_animator.HasState(BASE_LAYER_INDEX, stateHash))
        {
            return false;
        }

        _animator.Play(stateHash, BASE_LAYER_INDEX);
        return true;
    }

    // Animator는 자식(Graphics)에 있어 루트의 Awake보다 늦게 초기화될 수 있다. 그 시점에
    // controller/parameters를 읽으면 비어 보이므로, 실제 컨트롤러가 잡힌 뒤에만 캐시를 확정한다.
    private void EnsureParameterCache()
    {
        EnsureAnimatorController();

        if (!_animator.isInitialized)
        {
            return;
        }

        RuntimeAnimatorController controller = _animator.runtimeAnimatorController;

        if (controller == null)
        {
            _hasCachedParameters = false;
            _cachedAnimatorController = null;
            _animatorParameterHashes.Clear();
            return;
        }

        if (_hasCachedParameters && _cachedAnimatorController == controller)
        {
            return;
        }

        AnimatorControllerParameter[] parameters = _animator.parameters;

        if (parameters.Length == 0)
        {
            _hasCachedParameters = false;
            _cachedAnimatorController = controller;
            _animatorParameterHashes.Clear();
            return;
        }

        _cachedAnimatorController = controller;
        _hasCachedParameters = true;
        _hasAppliedState = false;
        _animatorParameterHashes.Clear();

        foreach (AnimatorControllerParameter parameter in parameters)
        {
            _animatorParameterHashes.Add(parameter.nameHash);
        }
    }

    private void EnsureAnimatorController()
    {
        if (_animator == null || _animator.runtimeAnimatorController != null)
        {
            return;
        }

        RuntimeAnimatorController controller = _fallbackAnimatorController;

#if UNITY_EDITOR
        if (controller == null)
        {
            controller = LoadEditorAnimatorController();
        }
#endif

        if (controller == null)
        {
            return;
        }

        _animator.runtimeAnimatorController = controller;
        _cachedAnimatorController = null;
        _hasCachedParameters = false;
        _hasAppliedState = false;
        _animatorParameterHashes.Clear();
    }

#if UNITY_EDITOR
    private RuntimeAnimatorController LoadEditorAnimatorController()
    {
        string controllerPath = CONTROLLER_ASSET_DIRECTORY + ResolveControllerAssetName() + CONTROLLER_ASSET_EXTENSION;
        return AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);
    }

    private string ResolveControllerAssetName()
    {
        string objectName = gameObject.name;

        if (objectName.EndsWith(CLONE_NAME_SUFFIX, StringComparison.Ordinal))
        {
            return objectName.Substring(0, objectName.Length - CLONE_NAME_SUFFIX.Length);
        }

        return objectName;
    }
#endif
}
