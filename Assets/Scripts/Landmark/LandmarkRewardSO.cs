using UnityEngine;

// 랜드마크 점령 완료 시 한 번만 지급되는 보상.
// 중복 지급 방지(수령 원장)는 LandmarkManager.TryClaim이 책임지므로 파생 클래스는
// "지급"만 구현한다 - 여기서 다시 수령 여부를 확인하지 않는다.
public abstract class LandmarkRewardSO : ScriptableObject
{
    // 실제로 지급했으면 true. 참조가 비어 지급하지 못한 경우 false를 반환해
    // 호출부가 수령 처리 대신 경고를 남길 수 있게 한다.
    public abstract bool Grant(LandmarkGrantContext context);
}
