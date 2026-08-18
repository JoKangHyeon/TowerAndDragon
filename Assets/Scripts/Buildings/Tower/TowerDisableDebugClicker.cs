using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// [테스트 전용] 밤에 타워를 가운데 버튼으로 클릭해 즉시 비활성화시킨다.
/// 타워 비활성화 연출(수비병이 튕겨 나오고 연기가 피어오르는 그림)을 몬스터를 기다리지 않고
/// 바로 확인하기 위한 도구다.
///
/// 체력을 직접 0으로 만들지 않고 <see cref="Tower.TakeDamage"/>로 치명타를 넣는다 -
/// 실제 전투와 같은 경로(Health.Died → Tower.HandleDisabled)를 타야 연출·부활·인구 회수까지
/// 실제 상황과 똑같이 재현되기 때문이다.
///
/// 가운데 버튼을 쓰는 이유: 좌클릭은 건설·인구 배치, 우클릭은 선택 해제·인구 회수가 이미 쓰고 있어
/// 어느 쪽에 얹어도 기존 조작과 충돌한다.
/// </summary>
public class TowerDisableDebugClicker : MonoBehaviour
{
    // 어떤 타워든 한 번에 눕히기 위한 값. 실제 최대 체력과 무관하게 확실히 죽도록 크게 잡는다.
    private const float LETHAL_DAMAGE = 999999f;

    [SerializeField] private GridMap _gridMap;
    [SerializeField] private MouseSelectController _mouseSelectController;

    [Tooltip("밤에만 동작하게 하는 판정용. 비워두면 낮/밤을 가리지 않는다.")]
    [SerializeField] private CycleManager _cycleManager;

    [Tooltip("끄면 클릭을 받지 않는다. 테스트가 끝나면 체크를 해제해 두면 된다.")]
    [SerializeField] private bool _isEnabled = true;

    private bool IsNight =>
        _cycleManager == null || _cycleManager.CurrentCycle == CycleManager.CycleState.Night;

    private void Update()
    {
        if (!_isEnabled || Mouse.current == null || !Mouse.current.middleButton.wasPressedThisFrame)
        {
            return;
        }

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        if (!WiringGuard.Require(_gridMap, nameof(_gridMap), this) ||
            !WiringGuard.Require(_mouseSelectController, nameof(_mouseSelectController), this))
        {
            return;
        }

        if (!IsNight)
        {
            Debug.Log("[TowerDisableDebugClicker] 밤에만 동작합니다.", this);
            return;
        }

        Vector3Int hoveredCell = _mouseSelectController.GetHoveredCell();
        Building building = _gridMap.GetBuildingAt(hoveredCell);

        if (building is not Tower tower)
        {
            Debug.Log($"[TowerDisableDebugClicker] {hoveredCell}에 타워가 없습니다.", this);
            return;
        }

        if (tower.IsDead || tower.IsReviving)
        {
            Debug.Log($"[TowerDisableDebugClicker] {tower.name}은 이미 비활성 상태입니다.", this);
            return;
        }

        Debug.Log($"[TowerDisableDebugClicker] {tower.name}을 강제로 비활성화합니다.", tower);
        tower.TakeDamage(new DamageInfo(LETHAL_DAMAGE));
    }
}
