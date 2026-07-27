using UnityEngine;

// 용 스킬 효과 베이스. 실제 게임플레이 훅(전투 패시브·액티브 수치·새끼용 설치형 등)은
// 대상 시스템이 없어 대부분 보류(stub)이다 - Docs/용_스킬트리_로드맵.md §8.
// 파생 클래스가 자기 조회 방식(활성 속성 조건부 값, SkillSO 참조 등)을 각자 정의한다.
public abstract class DragonSkillEffectSO : ScriptableObject
{
}
