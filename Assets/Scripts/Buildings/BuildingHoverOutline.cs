using System.Collections.Generic;
using EPOOutline;
using UnityEngine;

/// <summary>
/// 이 건물이 호버 아웃라인 대상임을 표시하고, 아웃라인을 켜고 끈다.
/// 붙어 있는 건물만 반응하므로 대상 목록이 코드가 아니라 프리팹에 있다 -
/// 대상을 늘리거나 줄일 때 스크립트를 고칠 필요가 없다.
///
/// 자기를 정적 목록에 등록하는 이유: 드라이버가 커서 아래를 찾을 때 배치된 건물 전체를
/// 훑지 않아도 되게 한다. GridMap.Buildings는 IEnumerable로 노출돼 foreach가 열거자를
/// 박싱하므로, 매 프레임 도는 경로에서 쓰면 프레임마다 힙 할당이 생긴다. 등록 목록은
/// List라 인덱서로 훑을 수 있고 대상이 한 자릿수라 순회 비용도 사실상 없다.
/// </summary>
[RequireComponent(typeof(Building))]
public sealed class BuildingHoverOutline : MonoBehaviour
{
    private static readonly List<BuildingHoverOutline> ACTIVE_TARGETS = new();

    /// <summary>지금 씬에 살아 있는 호버 아웃라인 대상. 드라이버가 이 목록만 훑는다.</summary>
    public static IReadOnlyList<BuildingHoverOutline> ActiveTargets => ACTIVE_TARGETS;

    [Tooltip("이 건물의 아웃라인. 같은 프리팹 안의 Outlinable을 연결하고, 그쪽 체크는 꺼 둔다 - 드라이버가 켠다. " +
        "색·두께는 그 Outlinable에서 건물별로 조절한다.")]
    [SerializeField] private Outlinable _outlinable;

    private Building _building;

    /// <summary>겹친 대상의 앞뒤를 가릴 때 쓴다(Building.CompareFrontToBack).</summary>
    public Building Building => _building;

    // 플레이모드 재진입 시 Reload Domain이 꺼져 있으면 이전 세션의 파괴된 항목이 목록에 남는다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => ACTIVE_TARGETS.Clear();

    private void Awake()
    {
        _building = GetComponent<Building>();
    }

    // 등록은 OnEnable에서 한다 - 드라이버의 첫 조회는 Update라 순서가 뒤집힐 여지가 없다.
    private void OnEnable()
    {
        ACTIVE_TARGETS.Add(this);

        // 프리팹에 켜진 채로 저장돼 있어도 시작 상태를 꺼진 것으로 보장한다.
        SetOutlined(false);
    }

    private void OnDisable()
    {
        ACTIVE_TARGETS.Remove(this);

        // 아웃라인이 켜진 채로 비활성화되면(타워 비활성화·철거) 그 상태가 그대로 남는다.
        SetOutlined(false);
    }

    public void SetOutlined(bool isOutlined)
    {
        if (!WiringGuard.Require(_outlinable, nameof(_outlinable), this))
        {
            return;
        }

        _outlinable.enabled = isOutlined;
    }

    /// <summary>커서가 이 건물의 스프라이트 몸통 안인지. 클릭 판정과 같은 함수를 쓴다.</summary>
    public bool ContainsWorldPoint(Vector3 worldPoint) =>
        _building != null && _building.ContainsWorldPoint(worldPoint);
}
