/// <summary>키 설정 섹션이 공유하는 스트링테이블 key 모음.
/// (UI_KeyBindingSection이 라벨을, UI_KeyBindingRow가 안내·경고 문구를 쓴다.)</summary>
public static class KeyBindingLocKeys
{
    // stringtable — config_key_waiting : "누를 키를 입력하세요… (Esc 취소)"
    public const string WAITING = "config_key_waiting";

    // stringtable — config_key_conflict : "이미 {0}에 사용 중인 키입니다."
    public const string CONFLICT = "config_key_conflict";

    // stringtable — 합성 바인딩(WASD 등)의 방향 이름.
    public const string PART_UP = "key_part_up";
    public const string PART_DOWN = "key_part_down";
    public const string PART_LEFT = "key_part_left";
    public const string PART_RIGHT = "key_part_right";

    // 합성 바인딩의 파트 이름(에셋에 저장된 값)과 위 key의 대응.
    private const string PART_NAME_UP = "up";
    private const string PART_NAME_DOWN = "down";
    private const string PART_NAME_LEFT = "left";
    private const string PART_NAME_RIGHT = "right";

    /// <summary>합성 바인딩의 파트 이름에 대응하는 loc key. 모르는 파트면 null.</summary>
    public static string PartLocKey(string partName)
    {
        return partName switch
        {
            PART_NAME_UP => PART_UP,
            PART_NAME_DOWN => PART_DOWN,
            PART_NAME_LEFT => PART_LEFT,
            PART_NAME_RIGHT => PART_RIGHT,
            _ => null,
        };
    }
}
