using UnityEngine;

// 타워처럼 취급되어 몬스터의 어그로를 끌고, 밤이 끝나면 스스로 파괴되는 성벽
public class StoneBarricade : Building, IMonsterTarget
{
    [SerializeField] private Health _health;

    [Tooltip("방벽의 기본 최대체력. 용 스킬트리의 방벽 강화 배율이 여기에 곱해진다.")]
    [SerializeField] [Min(1f)] private float _baseMaxHealth = 1f;

    // [IMonsterTarget 구현] 몬스터가 멈춰서 공격하게 만듭니다.
    public MonsterTargetType TargetType => MonsterTargetType.Tower;
    
    // [IAttackTarget 구현] 정확한 인터페이스 스펙을 준수합니다.
    public Transform TargetTransform => transform;
    public GameObject TargetObject => gameObject;
    public bool IsDead => _health == null || _health.IsDead;
    public new bool IsRemoveable => true; // 무조건 철거 가능하게 오버라이드

    protected override void Awake() 
    {
        base.Awake(); // GridMap 관련 초기화를 위해 base 호출
        
        if (_health != null)
        {
            _health.Died.AddListener(HandleDie);
        }
    }

    // 설치 직후 반드시 호출해야 한다. Health는 Initialize 전까지 MaxHealth가 0이라
    // IsDead가 곧바로 true가 되고, 몬스터가 이미 죽은 대상으로 보고 그냥 지나친다.
    public void Initialize(float healthMultiplier)
    {
        _health?.Initialize(_baseMaxHealth * healthMultiplier);
    }

    public void TakeDamage(DamageInfo damage)
    {
        if (_health != null && !IsDead)
        {
            _health.TakeDamage(damage.Amount); 
        }
    }

    private void HandleDie()
    {
        GridMap gridMap = Object.FindFirstObjectByType<GridMap>();
        if (gridMap != null)
        {
            Vector3Int gridPos = gridMap.ConvertWorldToGrid(transform.position);
            gridMap.RemoveBuilding(gridPos);
        }
    }

    public void RegisterAutoDestroy(CycleManager cycleManager)
    {
        if (cycleManager != null)
        {
            cycleManager.OnNightEnd.AddListener((cycle) => 
            {
                if (!IsDead) HandleDie();
            });
        }
    }
}