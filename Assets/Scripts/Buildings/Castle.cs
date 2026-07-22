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
    public bool IsDead => _health == null || _health.IsDead;
    public float CurrentHealth => _health == null ? 0 : _health.CurrentHealth;
    public float MaxHealth => _health == null ? 0 : _health.MaxHealth;
    public Transform TargetTransform => transform;
    public GameObject TargetObject => gameObject;


    /// <summary>현재 체력, 최대 체력 순으로 전달.</summary>
    public UnityEvent<float, float> HealthChanged;

    /// <summary>성 파괴 = 게임 패배.</summary>
    public UnityEvent Destroyed;

    private void Awake()
    {
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
        if (_gridMap == null)
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

        for (int dx = -_inactiveChunkRadius; dx <= _inactiveChunkRadius; dx++)
        {
            for (int dy = -_inactiveChunkRadius; dy <= _inactiveChunkRadius; dy++)
            {
                Vector2Int neighborCoord = homeChunk.ChunkCoord + new Vector2Int(dx, dy);
                _gridMap.SetChunkState(neighborCoord, ChunkState.Visible);
            }
        }
        _gridMap.SetChunkState(homeChunk.ChunkCoord, ChunkState.Conquered);
    }

    public void TakeDamage(DamageInfo damage)
    {
        // 죽음/음수 처리는 Health가 담당하므로 여기서 재검사하지 않는다.
        _health.TakeDamage(damage.Amount);
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
