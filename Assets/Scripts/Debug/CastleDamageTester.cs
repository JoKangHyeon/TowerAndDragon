using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// [테스트 전용] 스페이스바를 누르면 성에 데미지를 준다. 성 파괴 → 게임오버 흐름 확인용.
/// 최종 빌드 전에는 이 컴포넌트를 제거하거나 비활성화하면 된다.
/// </summary>
public class CastleDamageTester : MonoBehaviour
{
    [SerializeField] private Castle _castle;
    [SerializeField] private float _damagePerHit = 10f;

    private void Update()
    {
        if (_castle == null || Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            _castle.TakeDamage(new DamageInfo(_damagePerHit));
        }
    }
}
