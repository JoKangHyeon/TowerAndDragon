using System;
using UnityEngine;

// 건물 효과 한 종류의 표시 정보(아이콘 · 틴트 · 문구 키). 수치는 담지 않는다 - 밸런스 값의
// 단일 출처는 TerrainPenaltyData이며, 표시 시점에 런타임으로 읽어 문구에 채운다.
//
// 구조체가 아니라 클래스인 이유: IconColor의 기본값이 흰색이어야 하는데, 구조체 필드는
// 인스펙터에서 새 항목을 추가할 때 전부 0으로 채워져 알파 0(투명)이 되기 때문이다.
[Serializable]
public class BuildingEffectIconEntry
{
    [Tooltip("이 항목이 설명하는 효과 종류.")]
    public BuildingEffectKind Kind;

    [Tooltip("건물 위에 그릴 아이콘. 비워두면 아이콘 없이 수치 라벨만 그린다(전용 아트 배정 전 임시 상태).")]
    public Sprite Icon;

    [Tooltip("아이콘 틴트. 기존 스프라이트를 임시로 재활용할 때 부정적 효과임을 색으로 구분한다.")]
    public Color IconColor = Color.white;

    [Tooltip("효과 이름의 스트링테이블 키. 툴팁 각 줄의 앞부분에 쓴다.")]
    public string NameLocKey;

    [Tooltip("효과 설명의 스트링테이블 키. {0}에는 밸런스 테이블에서 읽은 수치가 들어간다.")]
    public string DescriptionLocKey;
}
