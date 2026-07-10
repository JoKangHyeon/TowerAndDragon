using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// [테스트 전용] 성에 데미지를 주어 체력바 감소·조각 연출을 확인하는 도구.
/// 지정한 키를 누르면 데미지를 준다. 인스펙터의 컨텍스트 메뉴로도 줄 수 있다.
/// 최종 빌드 전에는 이 컴포넌트를 제거하거나 비활성화하면 된다.
/// </summary>
public class CastleDamageTester : MonoBehaviour
{
    [SerializeField] private Castle _castle;
    [SerializeField] private float _damagePerHit = 10f;
    [SerializeField] private Key _damageKey = Key.Space;

    private void Update()
    {
        if (Keyboard.current == null || _castle == null)
        {
            return;
        }

        if (Keyboard.current[_damageKey].wasPressedThisFrame)
        {
            _castle.TakeDamage(new DamageInfo(_damagePerHit));
        }
    }

    [ContextMenu("데미지 주기")]
    private void DealDamage()
    {
        if (_castle != null)
        {
            _castle.TakeDamage(new DamageInfo(_damagePerHit));
        }
    }
}
