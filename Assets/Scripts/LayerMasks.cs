using UnityEngine;

/// <summary>
/// 이름으로 찾는 레이어 마스크를 한 번만 계산해 캐시한다.
///
/// [SerializeField] LayerMask 대신 여기서 계산하는 이유: 씬마다 흩어진 인스펙터 값은
/// 어긋나기 쉽고, WiringChecker는 LayerMask가 Nothing으로 남아도 "값이 있다"고 보므로
/// 씬 간 값 어긋남을 도구로 잡을 수 없다.
///
/// 프로퍼티 게터로 지연 계산하는 이유: LayerMask.GetMask/NameToLayer는 MonoBehaviour의
/// 생성자(정적 필드 초기화식 포함)에서 부르는 것을 Unity가 금지한다. 이 클래스처럼
/// 정적 필드 초기화식을 하나도 두지 않으면 C# 컴파일러가 정적 생성자를 만들지 않으므로,
/// 실제 GetMask 호출은 게터를 처음 부르는 시점(Update/LateUpdate 등, 항상 생성 이후)에만
/// 일어난다. 반대로 `private static readonly int X = LayerMask.GetMask(...)`처럼 초기화식에
/// 두면 그 타입을 참조하는 컴포넌트가 생성되는 도중 정적 생성자가 돌아 금지 구간에 걸리고,
/// 한 번 실패한 정적 생성자는 복구되지 않아 이후 그 타입의 모든 정적 접근이
/// TypeInitializationException을 반복해서 던진다.
/// </summary>
public static class LayerMasks
{
    private static int _enemy;
    private static bool _isEnemyResolved;

    public static int Enemy
    {
        get
        {
            if (!_isEnemyResolved)
            {
                _enemy = LayerMask.GetMask(Defines.ENEMY_LAYER_NAME);
                _isEnemyResolved = true;
            }

            return _enemy;
        }
    }
}
