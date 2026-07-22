using UnityEngine;
using UnityEngine.Rendering.Universal;

// Tower가 현재 타게팅 중인 대상을 향해 TowerLight(Light2D)를 회전시킨다.
// 타게팅 중인 대상이 없으면 빛을 끈다.
[RequireComponent(typeof(Light2D))]
public class TowerLightAimer : MonoBehaviour
{
    // Light2D 콘의 기본 방향이 로컬 +Y(위쪽)라 가정하고 보정하는 각도.
    private const float FORWARD_ANGLE_OFFSET_DEG = 90f;

    [SerializeField]
    private TowerAttack _towerAttack;

    // 타겟을 잃어도 이 시간 동안은 불을 유지해 재타게팅 시 깜빡임을 막는다.
    [SerializeField]
    private float _lightHoldSeconds = 0.25f;

    private Light2D _light2D;
    private float _lastValidTargetTime;

    private void Awake()
    {
        if (_towerAttack == null)
            _towerAttack = GetComponentInParent<TowerAttack>();

        _light2D = GetComponent<Light2D>();
        _lastValidTargetTime = float.NegativeInfinity;
    }

    // TowerAttack.Update()에서 타겟이 갱신된 이후에 회전을 반영하기 위해 LateUpdate 사용.
    private void LateUpdate()
    {
        BaseMonster target = _towerAttack != null ? _towerAttack.CurrentTarget : null;
        bool hasValidTarget = target != null && !target.IsDead;

        if (hasValidTarget)
        {
            _lastValidTargetTime = Time.time;
            _light2D.enabled = true;

            Vector2 direction = target.transform.position - transform.position;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - FORWARD_ANGLE_OFFSET_DEG;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
            return;
        }

        // 유효 타겟이 없어도 그레이스 기간 동안은 불을 유지(짧은 재타게팅 공백에 깜빡이지 않도록).
        bool withinHoldGracePeriod = Time.time - _lastValidTargetTime <= _lightHoldSeconds;
        _light2D.enabled = withinHoldGracePeriod;
    }
}
