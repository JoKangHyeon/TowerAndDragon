using UnityEngine;

// 몬스터 스프라이트를 진행 방향에 맞춰 좌우 반전한다. 이동 중에는 이동 방향의 X 성분을,
// 대상을 공격하려고 멈춰 선 동안에는 대상 방향을 본다. 방향을 알 수 없는 프레임에는
// 마지막으로 정한 방향을 그대로 유지한다(제자리에서 깜빡이며 뒤집히지 않도록).
public class MonsterSpriteFlipper : MonoBehaviour
{
    // 아이소메트릭 경로가 화면 기준으로 거의 수직으로 올라가는 구간에서는 X 성분이 0에 가까워진다.
    // 이 구간에서 부호가 튀며 반전이 떨리지 않도록 무시 구간을 둔다.
    private const float DIRECTION_DEADZONE = 0.01f;

    // 원본 스프라이트가 바라보는 방향. 왼쪽을 보고 그려진 스프라이트를 쓰는 프리팹은 이 값을 끈다.
    [SerializeField] private bool _isSourceSpriteFacingRight = true;

    private SpriteRenderer _renderer;
    private MonsterMovement _movement;
    private MonsterAttack _attack;
    private bool _isFacingRight;
    private bool _hasFacing;

    private void Awake()
    {
        // 모든 몬스터 프리팹은 스프라이트가 루트에 있다. GetComponentInChildren은 자기 자신을 먼저 보므로
        // 나중에 스프라이트를 자식으로 분리한 프리팹이 생겨도 그대로 동작한다.
        _renderer = GetComponentInChildren<SpriteRenderer>();
        _movement = GetComponent<MonsterMovement>();
        _attack = GetComponent<MonsterAttack>();
    }

    private void Update()
    {
        if (_renderer == null)
        {
            return;
        }

        float directionX = ResolveDirectionX();
        if (Mathf.Abs(directionX) < DIRECTION_DEADZONE)
        {
            return;
        }

        bool isFacingRight = directionX > 0;
        if (_hasFacing && isFacingRight == _isFacingRight)
        {
            return;
        }

        _hasFacing = true;
        _isFacingRight = isFacingRight;
        _renderer.flipX = isFacingRight != _isSourceSpriteFacingRight;
    }

    // 공격 대상이 이동 방향보다 우선한다 - 대상을 잡으면 MonsterAttack이 이동을 멈추므로
    // 그 동안 이동 방향은 0이고, 대상을 놓치면 다시 이동 방향으로 돌아온다.
    private float ResolveDirectionX()
    {
        Transform facingTarget = _attack != null ? _attack.FacingTargetTransform : null;
        if (facingTarget != null)
        {
            return facingTarget.position.x - transform.position.x;
        }

        return _movement != null ? _movement.MovementDirectionX : 0f;
    }
}
