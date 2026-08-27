using UnityEngine;

/// <summary>
/// 애니메이터 파라미터 존재 여부를 확인한다.
/// </summary>
/// <remarks>
/// Unity는 컨트롤러에 없는 파라미터에 Set*/Reset*을 걸면 매 호출마다 경고를 찍는다.
/// StringToHash 해시 오버로드도 마찬가지다. 아트가 아직 배정되지 않아 몬스터용이 아닌
/// 플레이스홀더 컨트롤러가 꽂힌 프리팹이 있으므로, 매 프레임 반영하는 파라미터는
/// 여기서 한 번 확인해 캐시한 뒤 호출을 건너뛴다.
///
/// Animator.parameters는 호출마다 배열을 새로 만들므로 매 프레임 쓰면 안 된다.
/// 반드시 Awake/Initialize에서 한 번만 호출해 결과를 보관할 것.
/// </remarks>
public static class AnimatorParameterUtility
{
    public static bool Has(Animator animator, int nameHash)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            return false;
        }

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.nameHash == nameHash)
            {
                return true;
            }
        }

        return false;
    }
}
