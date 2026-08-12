using System.Collections.Generic;
using UnityEngine;

// 인구 배치 모드가 켜져 있는 동안, 인구를 넣을 수 있는 건물마다 "배치/정원" 라벨을 하나씩 띄운다.
// 어떤 건물이 대상인지 판정하거나 갱신 시점을 정하지 않는다 - WorkerModeController가 대상 목록을
// 넘겨 Refresh를 호출하고, 모드 종료 시 Clear를 호출한다.
// ChunkInfoOverlayRenderer와 동일한 패턴(ComponentPool + 전체 재배치)을 따른다.
public class WorkerCountOverlayRenderer : MonoBehaviour
{
    [Tooltip("건물 위에 띄울 배치/정원 라벨 프리팹(월드 스페이스).")]
    [SerializeField]
    private WorkerCountLabel _labelPrefab;

    [Tooltip("건물 스프라이트 중앙 기준 라벨 미세 조정 오프셋. 기본은 정중앙(0).")]
    [SerializeField]
    private Vector3 _labelOffset = Vector3.zero;

    private ComponentPool<WorkerCountLabel> _labelPool;

    private void Awake()
    {
        _labelPool = new ComponentPool<WorkerCountLabel>(_labelPrefab, transform);
    }

    // 대상 목록(표시 위치 + 배치 대상)을 받아 위치마다 라벨을 하나씩 배치한다.
    // 위치를 호출자가 정하는 이유: 배치 대상이 건물만이 아니다 - 랜드마크는 Building이 아니라
    // 청크 중심에 라벨을 띄워야 하므로, 위치 계산을 아는 쪽(WorkerModeController)이 넘긴다.
    // labelColor는 조작 가능 여부(낮/밤)를 함께 알리기 위해 호출자가 정한다.
    public void Refresh(
        IReadOnlyList<(Vector3 WorldPosition, IPopulationAllocationTarget Target)> entries,
        Color labelColor)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            (Vector3 worldPosition, IPopulationAllocationTarget target) = entries[i];

            WorkerCountLabel label = _labelPool.Get(i);

            label.transform.position = worldPosition + _labelOffset;

            label.SetCount(target.AssignedPopulation, target.Capacity);
            label.SetColor(labelColor);
        }

        _labelPool.DeactivateFrom(entries.Count);
    }

    // 라벨은 건물 스프라이트의 정중앙에 붙인다. 피벗 위치나 스프라이트 높이가 건물마다 달라
    // (성·타워·생산시설) 고정 오프셋을 쓰면 낮은 건물의 라벨이 옆 건물 위로 떠버린다.
    // Building이 스프라이트를 찾는 방식과 같게 자식까지 훑는다(BabyDragonTower처럼 스프라이트를
    // 자식으로 분리한 건물 지원). 스프라이트가 없으면 트랜스폼 위치로 대체한다.
    public static Vector3 ResolveLabelPosition(Building building)
    {
        var spriteRenderer = building.GetComponentInChildren<SpriteRenderer>();

        return spriteRenderer != null
            ? spriteRenderer.bounds.center
            : building.transform.position;
    }

    // 인구 배치 모드가 꺼질 때 호출 - 모든 라벨을 숨긴다.
    public void Clear() => _labelPool.DeactivateAll();
}
