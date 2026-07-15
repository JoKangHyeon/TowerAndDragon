using UnityEngine;

// 임시 - 자원/인구 매니저가 생기면 이 클래스를 대체하고 참조만 바꾸면 된다.
public class TempResourcePool : MonoBehaviour
{
    [SerializeField]
    private ResourceCost _current;

    public ResourceCost Current => _current;
}
