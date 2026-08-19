/// <summary>
/// 방어막을 두른 개체(몬스터, 향후 타워 등)의 표시용 최소 계약.
/// 체력바 UI가 방어막의 구체 구현(MonsterShield 등)을 몰라도 회색 영역을 그릴 수 있게 한다.
/// IReviveProgress와 마찬가지로 폴링 전용 인터페이스로 둔다.
/// </summary>
public interface IShieldInfo
{
    // 이 개체가 방어막 자체를 보유하는가 (초기화 여부와 무관).
    bool HasShield { get; }

    // 방어막이 한 번도 깎이지 않았는가 - 체력바 숨김 판정에 쓴다.
    bool IsIntact { get; }

    float CurrentShield { get; }

    // 툴팁이 "20 / 50"처럼 남은 양을 전체와 견주어 적기 위해 필요하다.
    float MaxShield { get; }
}
