using UnityEngine;
/// <summary>
/// 한 번의 데미지 전달에 실리는 정보.
/// 지금은 데미지 양만 담지만, 이후 방어막 속성 상성 등을 여기에 추가해
/// 확장할 수 있도록 별도 타입으로 분리해 둔다.
/// </summary>
public readonly struct DamageInfo
{
    public float Amount {get;}
    public DragonType? Element {get;}
    public GameObject Source {get;}

    public DamageInfo(float amount)
        : this (amount, null, null)
    {
    }

    public DamageInfo(float amount, DragonType? element)
        : this (amount, element, null)
    {

    }

    public DamageInfo (float amount, DragonType? element, GameObject source)
    {
        Amount = amount;
        Element = element;
        Source = source;
    }

}
