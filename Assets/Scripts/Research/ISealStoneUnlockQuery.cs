// 봉인석 건설 해금 여부 조회 접점. 봉인석은 1~4차 순서로만 해금되므로(연구 트리가 순서를 강제),
// 차수(SealStoneData.Order)별로 해금 여부를 따로 묻는다. 본체(연구 노드/비용/UI)는 추후 별도 설계 -
// 지금은 PortalSealManager가 참조할 형태만 확정한다.
// 구현체가 없는 동안(null)은 전부 해금된 것으로 취급해 기존 씬이 그대로 동작한다.
public interface ISealStoneUnlockQuery
{
    bool IsUnlocked(int order);
}
