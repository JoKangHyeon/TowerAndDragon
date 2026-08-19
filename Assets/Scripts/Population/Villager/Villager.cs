using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
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
    Recall,

    /// <summary>목적지에 도착하면 잠깐 idle 후 사라진다. 추가 배치 인원의 이동 표현에 쓴다.</summary>
    Transit,

    /// <summary>타워가 비활성화될 때 사망 모션으로 튕겨져 나온다. 걸어가지 않고 포물선으로 날아간다.</summary>
    Ejected
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
    private const string DEATH_ANIM_KEY = "Death";

    // 포물선 높이 계수. 4t(1-t)는 t=0.5에서 최대 1이 되어, _ejectHeight가 그대로 최고점이 된다.
    private const float EJECT_ARC_FACTOR = 4f;
    private const float TRANSIT_ARRIVAL_IDLE_SECONDS = 0.15f;
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
    private static readonly int DEATH_ANIM_HASH = Animator.StringToHash(DEATH_ANIM_KEY);
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
        SendHome,
        HoldIdleThenSendHome
    }

    /// <summary>소멸 직전에 알린다 - VillagerDispatchSystem이 활성 목록에서 지우고 풀에 반납한다.</summary>
    public event Action<Villager> Finished;

    /// <summary>
    /// <see cref="Finished"/>가 정상 종료가 아니라 오브젝트 파괴(씬 언로드 등) 때문에 발화했는지.
    ///
    /// 파괴 중인 컴포넌트도 <c>OnDestroy</c> 안에서는 <c>!= null</c>이라 널 검사로는 구분되지 않는다.
    /// 이 구분을 놓치면 파괴된 인스턴스가 풀에 들어가 다음 획득이 죽은 오브젝트를 집는다.
    /// </summary>
    public bool WasDestroyed { get; private set; }

    [SerializeField] private RuntimeAnimatorController _fallbackAnimatorController;

    private VillagerMovement _movement;
    private Animator _animator;
    private MonsterSpriteFlipper _spriteFlipper;
    private SpriteRenderer[] _renderers;

    // 프리팹 상태의 렌더러 색. 페이드로 뭉갠 알파를 되돌릴 때 쓴다(그림자의 반투명도까지 그대로 보존).
    private Color[] _originalColors;
    private readonly HashSet<int> _animatorParameterHashes = new();
    private RuntimeAnimatorController _cachedAnimatorController;
    private Tween _fadeTween;

    private VillagerOrder _order;
    private VillagerCommand _command = VillagerCommand.None;
    private float _cheerSeconds;
    private float _fadeOutSeconds;
    private float _attackIntervalSeconds;
    private float _idleBeforeHomeSeconds;
    private float _ejectHeight;
    private float _ejectSeconds;
    private float _ejectLingerSeconds;
    private bool _hasFinished;
    private bool _hasCachedParameters;
    private bool _wantsMove;
    private bool _wantsWork;
    private bool _hasDesiredState;
    private bool _hasAppliedState;
    private bool _isFading;
    private float _fadeAlpha = 1f;
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

        // 페이드는 모든 렌더러를 같은 알파로 덮어쓴다. 그런데 그림자처럼 원래부터 반투명한 렌더러가
        // 섞여 있어(Shadow의 알파는 0.3 남짓), 되돌릴 때 일괄 1로 올리면 그림자가 짙어진다.
        // 프리팹 상태의 색을 그대로 잡아뒀다가 그 값으로 복원한다.
        _originalColors = new Color[_renderers.Length];

        for (int i = 0; i < _renderers.Length; i++)
        {
            _originalColors[i] = _renderers[i] != null ? _renderers[i].color : Color.white;
        }

        // 재사용할 때 지난 생애의 좌우 반전을 되돌리기 위해서만 잡는다. 없는 프리팹도 있어 널을 허용한다.
        _spriteFlipper = GetComponent<MonsterSpriteFlipper>();
    }

    // 알파를 되돌리는 일은 반드시 "비활성화되기 전"에 끝나야 한다.
    //
    // 프리팹이 KeepAnimatorStateOnDisable을 끄고 있어 Animator는 SetActive(true)마다 리바인딩하면서
    // 그 시점의 프로퍼티 값을 기본값으로 다시 스냅샷한다. 페이드로 알파가 0이 된 채 반납하면 그 0이
    // 새 기본값이 되고, 이후 Animator가 매 프레임 0으로 되돌려 버린다. 풀에서 꺼낸 뒤에 고치는 것으로는
    // 늦는 이유가 이것이다(PrefabPool.Acquire가 SetActive를 먼저 한다).
    private void OnDisable()
    {
        _fadeTween?.Kill();
        _fadeTween = null;
        _isFading = false;
        _fadeAlpha = 1f;
        RestoreOriginalColors();
    }

    private void RestoreOriginalColors()
    {
        if (_originalColors == null)
        {
            return;
        }

        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] != null)
            {
                _renderers[i].color = _originalColors[i];
            }
        }
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

    /// <summary>
    /// 풀에서 다시 꺼내 쓸 때 지난 생애의 흔적을 지운다. <see cref="Construct"/>·<see cref="Dispatch"/>
    /// 앞에서 한 번 부른다.
    ///
    /// 애니메이터 파라미터·트리거는 손대지 않는다 - 프리팹 전부가 KeepAnimatorStateOnDisable을 끄고
    /// 있어 비활성/재활성 과정에서 Unity가 알아서 되감는다.
    /// </summary>
    public void ResetForSpawn()
    {
        _hasFinished = false;
        WasDestroyed = false;
        _command = VillagerCommand.None;
        _idleBeforeHomeSeconds = 0f;

        // 페이드가 알파를 0까지 내려놓은 채 끝난다 - 되돌리지 않으면 재사용한 캐릭터가 투명한 채로 돌아다닌다.
        // 실제 복원은 OnDisable(반납 직전)이 담당하고, 여기서는 그 경로를 타지 않은 경우를 위한 보강이다.
        _fadeTween?.Kill();
        _fadeTween = null;
        _isFading = false;
        _fadeAlpha = 1f;
        RestoreOriginalColors();

        // SetLocomotionState는 "원하는 상태가 바뀔 때만" 재적용 플래그를 내린다. 재활성화된 애니메이터는
        // 기본 상태로 되감겨 있는데 지난 생애와 같은 상태를 원하면(상주 → 상주) 해시가 같아 재적용을
        // 건너뛰고 Idle에 굳어버린다. 그래서 적용 기록 자체를 비운다.
        _wantsMove = false;
        _wantsWork = false;
        _hasDesiredState = false;
        _hasAppliedState = false;
        _appliedStateHash = 0;

        _movement.ResetForSpawn();

        if (_spriteFlipper != null)
        {
            _spriteFlipper.ResetFacing();
        }
    }

    /// <summary>튕겨져 나오는 연출의 세기. <see cref="VillagerProfile.Ejected"/>에서만 쓴다.</summary>
    public void ConstructEject(float height, float seconds, float lingerSeconds)
    {
        _ejectHeight = height;
        _ejectSeconds = seconds;
        _ejectLingerSeconds = lingerSeconds;
    }

    public void Dispatch(in VillagerOrder order)
    {
        _order = order;

        // 튕겨 나오는 캐릭터는 걷지 않지만(HasOutboundLeg=false) 타워 위치에서 시작해야 하므로
        // 월드 좌표 워프는 그대로 태운다. SpreadOffset은 여기서 더하지 않는다 - 그건 날아갈 방향이다.
        if (order.HasFixedOriginWorldPosition &&
            (order.HasOutboundLeg || order.Profile == VillagerProfile.Ejected))
        {
            _movement.WarpWorld(order.OriginWorldPosition);
        }
        else
        {
            _movement.Warp(order.HasOutboundLeg ? order.OriginCell : order.WorkCell, order.SpreadOffset);
        }

        RunAsync(this.GetCancellationTokenOnDestroy()).Forget();
    }

    /// <summary>걸어가는 연출 없이 사라지게 한다. 타워/점령 정리·건물 철거 등에 쓴다.</summary>
    public void Despawn()
    {
        _command = VillagerCommand.Despawn;
    }

    /// <summary>성으로 걸어가 사라지게 한다. 상주 인구의 밤 귀가와 점령 완료 후 귀환에 쓴다.</summary>
    public void SendHome()
    {
        // 이미 사라지기로 한 개체를 되돌리지 않는다 - 밤 전환과 점령 완료가 같은 프레임에 겹칠 수 있다.
        if (_command == VillagerCommand.None)
        {
            _command = VillagerCommand.SendHome;
        }
    }

    /// <summary>제자리에서 잠깐 idle을 보여준 뒤 성으로 걸어가게 한다. 점령 완료 축하 연출에 쓴다.</summary>
    public void SendHomeAfterIdle(float idleSeconds)
    {
        if (_command == VillagerCommand.Despawn)
        {
            return;
        }

        _idleBeforeHomeSeconds = Mathf.Max(0f, idleSeconds);
        _command = VillagerCommand.HoldIdleThenSendHome;
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

            case VillagerProfile.Ejected:
                await EjectAsync(token);
                break;

            case VillagerProfile.Transit:
                PlayIdle();
                await UniTask.WaitForSeconds(TRANSIT_ARRIVAL_IDLE_SECONDS, cancellationToken: token);
                break;

            case VillagerProfile.Recall:
                // 목적지에 닿았으므로 더 할 일이 없다.
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

                await ExecuteCommandAsync(token);

                break;
        }

        await FinishAsync(token);
    }

    private async UniTask ExecuteCommandAsync(CancellationToken token)
    {
        switch (_command)
        {
            case VillagerCommand.SendHome:
                await WalkHomeAsync(token);
                break;

            case VillagerCommand.HoldIdleThenSendHome:
                await HoldIdleThenWalkHomeAsync(token);
                break;
        }
    }

    private async UniTask HoldIdleThenWalkHomeAsync(CancellationToken token)
    {
        PlayIdle();

        float elapsed = 0f;

        while (elapsed < _idleBeforeHomeSeconds && _command == VillagerCommand.HoldIdleThenSendHome)
        {
            elapsed += Time.deltaTime;
            await UniTask.Yield(PlayerLoopTiming.Update, token);
        }

        if (_command != VillagerCommand.Despawn)
        {
            await WalkHomeAsync(token);
        }
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

    // 타워가 비활성화될 때 수비병이 밖으로 튕겨 나오는 연출.
    // 걷는 이동(VillagerMovement)을 쓰지 않고 트랜스폼을 직접 포물선으로 던진다 - 지면을 따라
    // 걸어가는 것이 아니라 "날아가 떨어지는" 그림이라 이동 로직과 성격이 다르다.
    private async UniTask EjectAsync(CancellationToken token)
    {
        PlayDeath();

        // 날아갈 방향·거리는 디스패치가 정해 SpreadOffset으로 실어 보낸다(타워마다 다른 방향으로 튄다).
        Vector3 start = transform.position;
        Vector3 end = start + _order.SpreadOffset;
        float elapsed = 0f;

        while (elapsed < _ejectSeconds && _command != VillagerCommand.Despawn)
        {
            elapsed += Time.deltaTime;

            float progress = Mathf.Clamp01(elapsed / _ejectSeconds);

            // 수평은 등속, 수직은 포물선(4t(1-t)로 중간에 최고점). 아이소메트릭이라 화면 Y가 곧 높이다.
            float arc = EJECT_ARC_FACTOR * progress * (1f - progress);

            transform.position = Vector3.Lerp(start, end, progress) + new Vector3(0f, arc * _ejectHeight, 0f);

            await UniTask.Yield(PlayerLoopTiming.Update, token);
        }

        // 쓰러진 채로 잠깐 남아 있다가 사라진다(FinishAsync의 페이드가 이어받는다).
        await UniTask.WaitForSeconds(_ejectLingerSeconds, cancellationToken: token);
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

        _fadeTween?.Kill();
        _fadeAlpha = 1f;
        _isFading = true;

        _fadeTween = DOTween.To(
                () => _fadeAlpha,
                alpha =>
                {
                    _fadeAlpha = alpha;
                    SetAlpha(alpha);
                },
                0f,
                _fadeOutSeconds)
            .SetEase(Ease.Linear)
            .SetUpdate(UpdateType.Late)
            .SetLink(gameObject);

        await UniTask.WaitUntil(
            () => _fadeTween == null || !_fadeTween.IsActive() || _fadeTween.IsComplete(),
            cancellationToken: token);

        _fadeAlpha = 0f;
        SetAlpha(_fadeAlpha);
        _fadeTween = null;
        _isFading = false;
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

    // 오브젝트를 파괴하지 않고 알리기만 한다 - 인스턴스의 처분(풀 반납)은 만든 쪽인
    // VillagerDispatchSystem이 결정한다.
    //
    // 풀 반납이 일어나는 유일한 지점이 이 함수(정확히는 RunAsync 마지막의 FinishAsync)라는 것이
    // 재사용 안전성의 근거다. 반납 경로를 하나 더 만들면 아직 돌고 있는 이전 생애의 RunAsync가
    // 새 생애와 겹쳐 상태를 덮어쓴다.
    private void Finish()
    {
        if (_hasFinished)
        {
            return;
        }

        _hasFinished = true;
        Finished?.Invoke(this);
    }

    // 페이드 도중에 밖에서 파괴되면(씬 언로드 등) Finish가 돌지 못해 디스패치의 활성 목록에
    // 죽은 항목이 남는다 - 그 경우에도 반드시 알린다.
    private void OnDestroy()
    {
        _fadeTween?.Kill();
        _fadeTween = null;

        if (_hasFinished)
        {
            return;
        }

        _hasFinished = true;
        WasDestroyed = true;
        Finished?.Invoke(this);
    }

    private void PlayMove() => SetLocomotionState(isMoving: true, isWorking: false);

    private void PlayIdle() => SetLocomotionState(isMoving: false, isWorking: false);

    private void PlayWork() => SetLocomotionState(isMoving: false, isWorking: true);

    private void PlayDeath()
    {
        SetLocomotionState(isMoving: false, isWorking: false);
        SetTrigger(DEATH_ANIM_HASH);
    }

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

    private void LateUpdate()
    {
        if (_isFading)
        {
            SetAlpha(_fadeAlpha);
        }
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
