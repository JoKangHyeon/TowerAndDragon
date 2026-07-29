/// <summary>
/// 파괴(체력 0) 후 자동 부활을 진행 중인 개체의 진행도를 공급한다.
/// 체력바 UI가 Tower를 직접 알지 않고도 부활 게이지를 그릴 수 있도록 하는 최소 인터페이스.
/// 구현하지 않는 개체(몬스터 등)는 파괴 시 그대로 사라지므로 부활 게이지가 필요 없다.
/// </summary>
public interface IReviveProgress
{
    // 현재 부활 대기 중인지 - true인 동안 체력바는 부활 게이지로 전환해 표시한다.
    bool IsReviving { get; }

    // 부활 대기 경과 비율(0~1). IsReviving이 false일 때의 값은 의미가 없다.
    float ReviveProgress { get; }
}
