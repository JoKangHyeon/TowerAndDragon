using UnityEngine;

/// <summary>
/// 이 오브젝트가 켜져 있는 동안 카메라 조작(WASD·엣지 스크롤·좌드래그·휠 줌)을 막는다.
/// 새 모달 창을 만들면 이것 하나만 붙이면 된다 - 코드도 배선도 더 필요 없다.
///
/// <b>붙일 곳</b>: 스크립트 호스트가 아니라 <b>실제로 켜고 꺼지는 오브젝트</b>다.
/// 이 프로젝트의 창은 두 갈래다(UI_DragonWindow.cs:11-14 참고) -
/// 자기 자신을 끄는 창(설정·연구·도움말·불러오기)은 그 오브젝트에, 자식만 끄는 창(용 창의
/// Content)은 그 자식에 붙인다. 엉뚱한 쪽에 붙이면 창을 닫아도 카메라가 계속 막힌다.
///
/// <b>붙이면 안 되는 곳</b>: 창이 떠 있는 채로 월드를 다뤄야 하는 모드
/// (건설·점령·인구 배치·워커·스킬 타겟팅·새끼용 인벤토리와, 그리드 선택에 딸린 정보창들).
/// 그 모드들은 카메라를 움직이면서 쓰는 것이 정상 동작이다.
///
/// Open/Close가 아니라 OnEnable/OnDisable에 거는 이유는 GameSpeedManager의 창 일시정지 카운터와
/// 같다 - 닫는 경로가 여럿이라(ESC·X 버튼·바깥 클릭·다른 배타 모드가 밀어냄) 짝을 놓치기 쉽다.
/// Awake가 없으므로 "비활성으로 저장된 프리팹의 Awake가 첫 SetActive 안에서 도는" 함정
/// (CLAUDE.md의 _isOpen 가드)과는 무관하다.
/// </summary>
[DisallowMultipleComponent]
public class UI_CameraInputBlocker : MonoBehaviour
{
    private void OnEnable()
    {
        CameraInputBlockRegistry.Register(this);
    }

    private void OnDisable()
    {
        CameraInputBlockRegistry.Unregister(this);
    }
}
