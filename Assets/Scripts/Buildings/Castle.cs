using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>메인 성 개체. HP는 Health에 위임하고, 0이 되면 Destroyed(= 게임 패배)를 발생시킨다.</summary>
[RequireComponent(typeof(Health))]
public class Castle : Building, IAttackTarget
{
    private const float DEFAULT_MAX_HEALTH = 100f;

    // 추후 연구 시스템에서 시야 확장을 다루게 되면 이 값을 늘려서 재사용한다 - const로 고정하지 않음.
    private int _inactiveChunkRadius = 1;
    [SerializeField] private float _maxHealth = DEFAULT_MAX_HEALTH;
    [Tooltip("배치될 그리드. 지정하면 시작 시 타일맵 정중앙 기준 3x3을 점유한다(성 이미지는 이동하지 않으니 직접 맞춰 배치). 비워두면 그리드 등록을 건너뛴다.")]
    [SerializeField] private GridMap _gridMap;

    [Tooltip("파괴 연출용 Animator. 평소 비활성으로 두고, 파괴 시 활성화하면 castle_broken이 1회 재생된다.")]
    [SerializeField] private Animator _brokenAnimator;
    [Tooltip("성 파괴 시 게임오버를 처리할 매니저.")]
    [SerializeField] private GameManager _gameManager;

    private Health _health;
    private Vector2Int _homeChunkCoord;
    private bool _hasHomeChunk;
    public bool IsDead => _health == null || _health.IsDead;
    public float CurrentHealth => _health == null ? 0 : _health.CurrentHealth;
    public float MaxHealth => _health == null ? 0 : _health.MaxHealth;
    public Transform TargetTransform => transform;
    public GameObject TargetObject => gameObject;


    /// <summary>현재 체력, 최대 체력 순으로 전달.</summary>
    public UnityEvent<float, float> HealthChanged;

    /// <summary>성 파괴 = 게임 패배.</summary>
    public UnityEvent Destroyed;

    protected override void Awake()
    {
        base.Awake();
        _health = GetComponent<Health>();
        _health.HealthChanged.AddListener(HandleHealthChanged);
        _health.Died.AddListener(HandleDestroyed);

        // 파괴 연출 Animator는 시작 시 꺼둔다 → 사망 시(HandleDestroyed)에만 켜서 1회 재생.
        if (_brokenAnimator != null)
        {
            _brokenAnimator.enabled = false;
        }
    }

    private void Start()
    {
        // Awake가 아닌 Start에서 초기화 → 다른 컴포넌트가 초기 HealthChanged를 받도록.
        _health.Initialize(_maxHealth);
        RegisterCenterFootprint();
    }

    // 그리드가 지정돼 있으면 타일맵 정중앙 기준 3x3 footprint를 점유 등록한다.
    // 성 이미지(transform)는 옮기지 않는다 — 시각적 정렬은 인스펙터에서 직접 맞춘다.
    // (GridMap.Awake가 그리드 생성을 끝낸 뒤인 Start에서 실행되므로 그리드가 준비돼 있다.)
    private void RegisterCenterFootprint()
    {
        if (!WiringGuard.Optional(_gridMap, nameof(_gridMap), this))
        {
            return;
        }

        Vector3Int center = _gridMap.GetCenterCell();
        Vector3Int anchor = center - FootprintShape.CenterOffset;

        if (!_gridMap.RegisterFootprint(this, anchor))
        {
            Debug.LogWarning($"[Castle] 그리드 점유 등록 실패 - center {center}, anchor {anchor}.");
            return;
        }

        SetUpInitialTerritory(center);
    }

    // --- 게임 시작 시 초기 영역 세팅 ---

    private void SetUpInitialTerritory(Vector3Int castleCenter)
    {
        Chunk homeChunk = _gridMap.GetChunkAt(castleCenter);
        if (homeChunk == null)
            return;

        _homeChunkCoord = homeChunk.ChunkCoord;
        _hasHomeChunk = true;

        RevealSurroundingChunks(0);
        _gridMap.SetChunkState(_homeChunkCoord, ChunkState.Conquered);
    }

    /// <summary>
    /// 성 주변 (기본 반경 + bonusRadius) 청크를 공개한다. 연구로 시야가 확장될 때마다
    /// 재호출 가능하다. 이미 Visible/Conquered인 청크는 건드리지 않는다 -
    /// GridMap.SetChunkState는 상태를 무조건 덮어쓰므로 가드 없이 재호출하면
    /// 이미 점령한 청크가 강등되고 점령 테두리 렌더링이 깨진다.
    /// </summary>
    public void RevealSurroundingChunks(int bonusRadius)
    {
        if (!_hasHomeChunk)
        {
            return;
        }

        int depth = _inactiveChunkRadius + bonusRadius;

        // 정사각형 반경 대신 접경 단계 수로 넓힌다 - 청크가 임의 모양이라 좌표 거리는 의미가 없다.
        _gridMap.CollectChunksWithinDepth(_homeChunkCoord, depth, _revealBuffer);

        // Hidden인 청크만 남긴다 - SetChunkStatesBulk는 상태를 무조건 덮어쓰므로,
        // 이미 점령한 청크가 섞이면 Visible로 강등되고 점령 테두리가 깨진다.
        _revealBuffer.RemoveAll(chunkCoord =>
        {
            Chunk chunk = _gridMap.GetChunk(chunkCoord);
            return chunk == null || chunk.CurrentState != ChunkState.Hidden;
        });

        // 청크마다 SetChunkState를 부르면 OnChunkStateChanged가 그 횟수만큼 발행되고 점령 테두리
        // 렌더러가 매번 맵 전체를 다시 트레이싱한다 - 한 번에 몰아서 전환한다.
        _gridMap.SetChunkStatesBulk(_revealBuffer, ChunkState.Visible);
    }

    // 시야 확장 대상 청크 버퍼 - 연구로 시야가 늘 때마다 재호출되므로 재사용한다.
    private readonly List<Vector2Int> _revealBuffer = new();

    // 튜토리얼이 배선한다 - 배선되지 않은 씬에서는 null로 남아 언제나 피해를 받는다(기존 동작 유지).
    public ICastleDamageBlockQuery DamageBlockQuery { get; set; }

    public void TakeDamage(DamageInfo damage)
    {
        // 아직 무너지면 안 되는 구간(튜토리얼 1~2일차)에서는 치명타만 흘려보낸다.
        if (DamageBlockQuery != null && !DamageBlockQuery.CanTakeDamage(damage.Amount))
        {
            return;
        }

        // 죽음/음수 처리는 Health가 담당하므로 여기서 재검사하지 않는다.
        _health.TakeDamage(damage.Amount);
    }

    /// <summary>세이브 복원 전용. Start의 Initialize가 만피로 세팅한 체력을 저장값으로 되돌린다.</summary>
    public void RestoreHealth(float currentHealth)
    {
        _health.RestoreCurrentHealth(currentHealth);
    }

    // convenience_castle_regen_1(매일 낮 자동 회복)과 convenience_castle_repair(자원 소모 즉시 수리)가
    // 공유하는 회복 경로. 사망 상태 가드는 Health.Heal이 담당한다.
    public void Repair(float amount)
    {
        _health.Heal(amount);
    }

    private void HandleHealthChanged(float current, float max)
    {
        HealthChanged?.Invoke(current, max);
    }

    private void HandleDestroyed()
    {
        // 파괴 연출: 비활성 Animator를 켜면 castle_broken 클립이 1회 재생된다.
        if (_brokenAnimator != null)
        {
            _brokenAnimator.enabled = true;
        }

        // 게임오버는 성이 GameManager에 호출을 넘긴다.
        if (_gameManager != null)
        {
            _gameManager.GameOver();
        }

        Destroyed?.Invoke();
    }

    private void OnDestroy()
    {
        if (_health != null)
        {
            _health.HealthChanged.RemoveListener(HandleHealthChanged);
            _health.Died.RemoveListener(HandleDestroyed);
        }
    }
}
