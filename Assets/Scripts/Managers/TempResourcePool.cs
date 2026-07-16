using UnityEngine;

// 임시 - 자원/인구 매니저가 생기면 이 클래스를 대체하고 참조만 바꾸면 된다.
public class TempResourcePool : MonoBehaviour
{
    [SerializeField]
    private ResourceCost _current;

    public ResourceCost Current => _current;

    // 원정 발송 성공 시 비용만큼 실제로 차감.
    public void Spend(ResourceCost cost)
    {
        _current = _current.Subtract(cost);
    }

    // 점령 완료 시 인구 보상만큼 실제로 지급.
    public void GrantPopulation(int amount)
    {
        _current.Population += amount;
    }
}
