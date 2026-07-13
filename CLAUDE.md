# CLAUDE.md — Tower and Dragons

팀원과 AI 에이전트가 공통으로 따르는 프로젝트 규칙 문서입니다.

## 프로젝트 개요

- 타워디펜스 + 도시건설, 싱글플레이, Windows/PC
- Unity · C#, 데이터 기반 설계(ScriptableObject/CSV) — 미션 필수 요건
- 수성록 프로젝트 (기업협약, 3팀: 조강현·김지해·이하늘·나상욱)
- 컨셉: "낮의 선택이 밤의 생존을 결정한다" (용의 운용 × 인구의 배분 × 전략적 확장)

## 기획 문서 (Single Source of Truth)

- 상세 기획: `Docs/기획종합_v2.md` — 기획 내용을 이 문서에 복제하지 말 것
- 자원 흐름: `Docs/자원순환_플로우차트.mermaid` (렌더본: 같은 이름 .svg)
- 문서 우선순위: 컨셉발표 PPT(외부 공유) > 기획종합_v2 > 기타
- 기획서 원본은 기획 담당(에이전트)이 관리하며, 확정본만 `Docs/`에 반영
- 기획서의 `[미정]` 항목은 임의로 확정 구현 금지 — 팀에 확인 후 진행

## 코드 작업용 기획 요약 (최소한)

- 하루: 낮(운영, 시간 무제한) → 밤(웨이브 전멸 시 승리) → 밤 종료 후 정산
- 주기 4회 · 주기 말 보스 웨이브 · 포탈 4개 순차 개방
- 핵심 시스템: 어미용(5속성) / 새끼용 / 인구 배분(생산·방어·점령) /
  점령(밤 방어 성공 = 획득) / 타워(인구 On/Off, 파괴 대신 비활성화) /
  자원(기본4 + 특화4 + 슬라임) / 연구 4갈래
- 승리: 최종 보스 격파 or 4포탈 동시 봉인 / 패배: 메인 성 파괴

## 개발 환경

- Unity 6000.3.15f1 (Unity 6.3)
- URP 2D + Isometric Tilemap
- Input System (신형, `InputSystem_Actions.inputactions`)
- 빌드 타겟: Windows

## 폴더 구조 (Assets/)

- `Scenes/`   : 씬
- `Scripts/`  : C# 스크립트
- `Settings/` : URP·프로젝트 설정
- `Imported/` : 외부 번들 에셋 (수정 금지, 원본 유지)
- `Data/`     : ScriptableObject·CSV
- `Prefabs/`  : 프리팹

## 도구 규칙 — Coplay MCP

- Unity 에디터 작업(씬·프리팹·컴포넌트·에셋 조작, 컴파일 에러 확인,
  플레이 테스트)은 Coplay MCP를 사용합니다.
- 단, Coplay 토큰을 소모하는 생성형 기능은 사용 금지:
  3D 모델·텍스처·이미지·음악·SFX·TTS 생성, 오토 리깅, 애니메이션 라이브러리 검색
- 에셋이 필요하면 생성하지 말고 팀에 요청합니다.

## 비동기 처리 규칙 — UniTask

- 비동기 로직은 Coroutine 대신 UniTask를 사용합니다. (`com.cysharp.unitask` 설치됨)
  - 새 코드에서 `StartCoroutine`/`IEnumerator` 기반 비동기 작성 금지
  - 기존 Coroutine 코드는 해당 파일을 수정할 때 UniTask로 전환
- 사용 지침:
  - 시간 대기: `WaitForSeconds` 대신 `UniTask.WaitForSeconds(...)` / `UniTask.Delay(...)`
  - 프레임 대기: `yield return null` 대신 `await UniTask.Yield()` / `UniTask.NextFrame()`
  - 조건 대기: `await UniTask.WaitUntil(...)`
  - GameObject 파괴 시 자동 취소되도록 `this.GetCancellationTokenOnDestroy()`를 전달합니다.
  - fire-and-forget 호출은 `.Forget()`을 명시합니다.
  - `async void` 금지 — 반환 타입은 `UniTask` / `UniTaskVoid`를 사용합니다.

## Git 커밋 규칙

커밋 전 다음 사항을 확인합니다.

1. AI가 생성한 모든 코드는 직접 검토한 후 커밋합니다.
2. PR에 포함된 코드는 실행 가능한 상태여야 합니다.
   (컴파일 에러 0, 플레이 모드 진입 가능)
3. 코드 본문에 string 리터럴을 포함하지 않습니다.
   - 3.1. 언어별로 달라지는 문자열은 스트링테이블 key로 변환받습니다.
     key 상수에는 접미사 `_LOC_KEY`를 붙입니다.
     번역 미확정 항목은 스트링테이블의 "값" 앞에 `[미정]`을 붙입니다. (key는 변경하지 않음)
     - 3.1.1. 프로퍼티로 감싸 언어별 문자열을 바로 가져오도록 강력히 권장합니다.
   - 3.2. 2개 이상의 스크립트에서 사용되는 문자열은 Defines static class에 지정합니다.
4. 코드 본문에 리터럴 상수를 포함하지 않습니다.
   - 4.1. 0 비교 대신 IsDead 같은 의도가 드러나는 프로퍼티를 만드세요.
   - 4.2. 불가피한 0, 1을 제외한 리터럴 상수는 금지. 별도 상수로 선언하세요.
5. 예외 — 3·4번 미적용: Debug.Log·예외 메시지, 에디터 전용 코드, 테스트 코드
6. 상수 네이밍: UPPER_SNAKE_CASE (POCU 표준 준거)
7. 변수 네이밍:
   - private 필드: `_camelCase` / public 필드·프로퍼티: `PascalCase`
   - 지역 변수·파라미터: `camelCase`
   - 인스펙터 노출은 public 대신 `[SerializeField] private` 권장

### 리터럴 처리 예시

```csharp
// stringtable — ladder_message : "사다리의 최소 높이는 {0} 이상이어야 합니다."
const string LADDER_MESSAGE_LOC_KEY = "ladder_message";
const int LADDER_MIN_HEIGHT = 1;

static string LadderMessage =>
    string.Format(StringTable.GetString(LADDER_MESSAGE_LOC_KEY), LADDER_MIN_HEIGHT);
```

## 협업 규칙

- 기획서는 담당자(에이전트)만 수정. 코드 작업 전 기획서 최신본 확인
- 문서·커밋 메시지는 한국어
- 마일스톤: 프로토타입 7/8–7/31 → 알파 ~8/14 → 베타 ~8/28 → 제출 9/3
