using UnityEngine;

/// <summary>
/// 한 번의 공격 적용에 실리는 부가 정보. 효과가 시전자 위치·소유자 등을
/// 필요로 할 때 참조한다. 지금은 시전자만 담지만, 크리티컬 여부·명중 지점 등을
/// 이후 확장할 수 있도록 별도 타입으로 분리해 둔다.
/// </summary>
public readonly struct AttackContext
{
    public GameObject Source { get; }

    public AttackContext(GameObject source)
    {
        Source = source;
    }
}
