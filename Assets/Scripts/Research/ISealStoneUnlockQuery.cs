// 봉인석 건설 해금 여부 조회 접점. 본체(연구 노드/비용/UI)는 추후 별도 설계 -
// 지금은 PortalSealManager가 참조할 형태만 확정한다.
// 구현체가 없는 동안(null)은 해금된 것으로 취급해 기존 씬이 그대로 동작한다.
public interface ISealStoneUnlockQuery
{
    bool IsSealStoneUnlocked { get; }
}
