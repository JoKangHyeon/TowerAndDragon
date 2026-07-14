using UnityEngine;

// Tower가 현재 타게팅 중인 대상을 향해 TowerLight(Light2D)를 회전시킨다.
public class TowerLightAimer : MonoBehaviour
{
    // Light2D 콘의 기본 방향이 로컬 +Y(위쪽)라 가정하고 보정하는 각도.
    private const float FORWARD_ANGLE_OFFSET_DEG = 90f;

    [SerializeField]
    private TowerAttack _towerAttack;

    private void Awake()
    {
        if (_towerAttack == null)
            _towerAttack = GetComponentInParent<TowerAttack>();
    }

    // TowerAttack.Update()에서 타겟이 갱신된 이후에 회전을 반영하기 위해 LateUpdate 사용.
    private void LateUpdate()
    {
        BaseMonster target = _towerAttack != null ? _towerAttack.CurrentTarget : null;
        if (target == null || target.IsDead)
            return;

        Vector2 direction = target.transform.position - transform.position;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - FORWARD_ANGLE_OFFSET_DEG;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }
}
