/// <summary>
/// 지금 어미용 진행(스킬 해금·속성 변경)을 해도 되는지 묻는 관문.
///
/// 안내가 아직 가르치지 않은 것을 미리 해버리면, 그것을 시키는 단계에 도달했을 때 이미 되어 있어
/// 완료 조건이 영영 오지 않는다 - 1·2일차에 용 창을 열어 스킬을 미리 해금해 두면 3일차 해금 단계에서
/// 그대로 갇혔다. 속성 변경도 같은 구조다.
///
/// 막는 판정과 허용 판정을 나눈 이유는 UIManager의 단축키 관문과 같다 - "안내가 도는 동안에는 막는다"와
/// "지금 이 단계만은 시킨 것이니 허용한다"는 서로 반대 방향이라, 한 판정에 섞으면 표현할 수 없다.
///
/// 사유를 알리는 것은 막은 쪽의 일이다(<see cref="NotifyDragonProgressionBlocked"/>) - 막는 이유를
/// 아는 쪽만 문구를 고를 수 있고, 막힌 조작마다 토스트 배선을 늘리지 않아도 된다.
/// </summary>
public interface IDragonProgressionGateQuery
{
    /// <summary>지금은 어미용 진행을 스스로 할 때가 아니라고 막는가.</summary>
    bool BlocksDragonProgression();

    /// <summary>막힌 중에도 지금 스킬을 해금하라고 시키는 단계인가.</summary>
    bool AllowsDragonSkillUnlock();

    /// <summary>막힌 중에도 지금 속성을 바꾸라고 시키는 단계인가.</summary>
    bool AllowsDragonAttributeChange();

    /// <summary>막았다는 것을 플레이어에게 알린다. 실제 시도가 거절된 순간에만 불린다.</summary>
    void NotifyDragonProgressionBlocked();
}
