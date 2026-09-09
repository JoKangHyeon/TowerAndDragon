using UnityEngine;

#if UNITY_EDITOR
using UnityEngine.InputSystem;
#endif

/// <summary>
/// [에디터 테스트 전용] 스페이스바를 누르면 성에 데미지를 준다. 성 파괴 → 게임오버 흐름 확인용.
/// 본문 전체가 #if UNITY_EDITOR 안에 있어 빌드에서는 컴파일되지 않는다
/// (클래스 껍데기만 남아 씬/프리팹의 컴포넌트 참조가 Missing Script가 되지 않는다).
/// </summary>
public class CastleDamageTester : MonoBehaviour
{
#if UNITY_EDITOR
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
#endif
}
