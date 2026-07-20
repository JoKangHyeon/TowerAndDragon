using UnityEngine;

// 자원 분류. 기본(중앙 초원 생산) / 특화(외곽 바이옴 점령 필요).
public enum ResourceCategory
{
    Basic,
    Specialized,
}

/// <summary>자원 1종의 메타데이터(표시 이름 key/아이콘/분류). 보유량 관리는 ResourceManager가 담당한다.</summary>
[CreateAssetMenu(menuName = "TowerAndDragon/Resource Data", fileName = "ResourceData")]
public class ResourceData : ScriptableObject
{
    [Tooltip("이 데이터가 나타내는 자원 종류. 단일 종류만 지정한다(복합 플래그 금지).")]
    [SerializeField] private ResourceType _type;

    [Tooltip("자원 이름 스트링테이블 key. 코드에 직접 이름 문자열을 넣지 않는다.")]
    [SerializeField] private string _nameLocKey;

    [SerializeField] private Sprite _icon;

    [SerializeField] private ResourceCategory _category;

    public ResourceType Type => _type;
    public string NameLocKey => _nameLocKey;
    public Sprite Icon => _icon;
    public ResourceCategory Category => _category;
}
