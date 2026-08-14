---
name: run-wiring-checker
description: 빌드 설정에 포함된 모든 씬을 순회하며 인스펙터 와이어링 누락(미연결 참조·Missing Script·끊긴 UnityEvent 리스너)을 찾아 리포트로 뽑는다. "와이어링 검사", "씬 참조 누락 확인", "Missing Script 찾기", "빌드 씬 점검", run wiring checker, check scene wiring, find unassigned inspector references, missing script scan 요청에 사용.
---

# Wiring Checker

빌드 설정(`EditorBuildSettings`)에 등록되고 **활성화된 모든 씬**을 훑어서 "인스펙터에 꽂혀 있어야 할
참조가 비어 있는" 곳을 찾아낸다. 런타임 가드인 `WiringGuard`는 그 코드가 *실행돼야* 알려주지만,
이 도구는 플레이하지 않고 씬 데이터만 정적으로 읽는다.

모든 경로는 프로젝트 루트(`C:\Users\jokh9\Documents\TowerAndDragon`) 기준이다.

## 구성

| 파일 | 역할 |
|---|---|
| `Assets/Scripts/Util/Editor/WiringChecker.cs` | 검사 엔진 (씬 순회 + 직렬화 프로퍼티 스캔) |
| `Assets/Scripts/Util/Editor/WiringCheckerRunner.cs` | 메뉴·콘솔 요약·리포트 파일 출력 |
| `Assets/Scripts/Util/WiringOptionalAttribute.cs` | 비워둬도 되는 필드 표기 (런타임 어셈블리) |
| `.claude/skills/run-wiring-checker/RunWiringCheck.cs` | Coplay `execute_script` 진입점 (아래 에이전트 경로) |

## 검사 항목

- **UnresolvedScriptGuid** — 씬 YAML의 `m_Script` GUID를 가진 스크립트가 프로젝트에 없음 (파일 단위 검사)
- **MissingScript** — 컴포넌트 슬롯이 비어 있음 (메모리 단위 검사)
- **UnassignedReference** — 직렬화된 오브젝트 참조가 비어 있음 (중첩 클래스·배열 원소까지 추적)
- **BrokenReference** — 인스턴스 ID는 남았는데 대상 에셋이 삭제됨
- **EventListener** — UnityEvent 영속 리스너의 대상이 비었거나 메서드명이 빈 항목

검사 대상은 `Assets/` 아래(단 `Assets/Imported/` 제외)에서 온 `MonoBehaviour`뿐이다.

## 실행 (에이전트 경로)

Unity 에디터가 열려 있어야 하고, **플레이 모드가 아니어야** 한다. Coplay MCP로 진입점을 실행한다.

```
mcp__coplay-mcp__execute_script
  filePath:   .claude/skills/run-wiring-checker/RunWiringCheck.cs
  methodName: Execute
```

반환값이 곧 요약이다. 실제 출력:

```
[WiringChecker] 씬 3개 검사 - 에러 41건, 경고 48건
  Assets/Scenes/StartScene.unity - 에러 3, 경고 4
  Assets/Scenes/SampleScene.unity - 에러 10, 경고 12
  Assets/Scenes/Tutorial.unity - 에러 28, 경고 32
리포트: Temp\WiringCheckReport.md
```

전체 목록은 리포트 파일을 읽는다. 항목마다 해당 필드의 `[Tooltip]`이 `//` 뒤에 붙어 나오므로
"이건 원래 비워두는 필드인가"를 소스를 열지 않고 판단할 수 있다.

```
Read C:\Users\jokh9\Documents\TowerAndDragon\Temp\WiringCheckReport.md
```

리포트 한 줄의 형태:

```
- [UnassignedReference] UI_CastleHealth._amountText @ Grid_HeightVariant/Structures/HP_Castle_ingame  // 현재 체력/최대 체력을 표시하는 텍스트.
- [UnresolvedScriptGuid] m_Script guid db3200645c162584daeae3f0b9ed9bd1 @ Assets/Scenes/SampleScene.unity:1243  // 이 GUID를 가진 스크립트가 프로젝트에 없습니다. ...
```

코드 변경 후에는 먼저 리프레시하고 컴파일을 확인한다. `AssetDatabase.Refresh()`만 쓴다
(`ImportAssetOptions.ForceUpdate`는 아래 Gotchas 참고).

```
mcp__coplay-mcp__check_compile_errors
```

## 실행 (사람 경로)

Unity 메뉴에서 직접 돌린다.

- `Tools/Wiring Checker/빌드 씬 전체 검사` — 위와 동일. 콘솔 요약 + `Temp/WiringCheckReport.md`
- `Tools/Wiring Checker/현재 씬만 검사` — 지금 열린 씬만. 항목마다 개별 로그를 남기므로
  **콘솔 항목을 클릭하면 해당 게임오브젝트로 이동**한다. 리포트 파일은 쓰지 않는다.

## 오탐 줄이기 — `[WiringOptional]`

비워둬도 정상 동작하는 필드는 선언부에 `[WiringOptional]`을 붙인다. 에러가 아니라 경고로 내려간다.
`WiringGuard.Optional`과 짝을 이루는 정적 표기다.

```csharp
[Tooltip("랜드마크 시각물을 담을 부모. 비우면 이 오브젝트 아래에 만든다.")]
[WiringOptional]
[SerializeField] private Transform _landmarkRoot;
```

배열·리스트 필드에 붙이면 **그 안의 원소 미연결까지** 함께 경고로 취급한다
(`_chains.Array.data[3].Trigger._targetFactoryData` 같은 중첩 경로도 체인 전체를 본다).

판단 기준은 그 필드의 `[Tooltip]`이다. "비우면 ~ 한다 / 비워두면 ~ / 비어 있으면 ~ / 없어도 동작한다"처럼
**빈 값의 동작이 명시돼 있으면** Optional, 기능 설명만 있으면 필수다. 2026-08-14 기준 이 규칙으로
28개 필드가 표기돼 있다.

```bash
# 현재 표기된 필드 목록 (검사 엔진 자체의 주석·리포트 문구가 섞이므로 Util/ 제외)
grep -rn "\[WiringOptional\]" --include=*.cs Assets/Scripts | grep -v "Assets/Scripts/Util/"
```

## Gotchas

- **이미 열려 있는 씬은 열린 상태 그대로 검사한다.** 닫혀 있는 씬만 Additive로 열었다가 닫으므로
  편집 중인 미저장 변경분을 잃지 않는다. 저장 프롬프트도 뜨지 않는다
  (뜨면 Coplay 호출이 전부 타임아웃된다).
- **메모리의 씬은 디스크의 씬보다 멀쩡해 보일 수 있다.** 스크립트를 옮기거나 머지가 잘못돼 GUID가
  끊겨도, 이 머신 `Library/`에 옛 매핑이 캐시돼 있으면 에디터에서는 정상 컴포넌트로 보인다.
  새로 클론한 팀원에게서만 Missing Script로 터진다. 그래서 `UnresolvedScriptGuid` 검사는
  메모리가 아니라 **씬 파일 텍스트를 직접 읽는다.** 실제로 `SampleScene.unity:1243`의
  `db3200645c162584daeae3f0b9ed9bd1`(CameraController)가 이 경로로만 잡혔다.
- **`AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate)`를 쓰지 말 것.** 끊긴 GUID를 메모리에서
  되살려 버려서 `MissingScript`와 그 컴포넌트를 참조하던 필드들이 한꺼번에 조용히 사라진다
  (이 세션에서 SampleScene 발견 건수가 24 → 21로 줄었다). 게다가 씬을 dirty로 만든다.
  그냥 `AssetDatabase.Refresh()`를 쓴다.
- **최상위 `m_*` 필드는 전부 무시한다.** `m_PrefabAsset` / `m_CorrespondingSourceObject` /
  `m_PrefabInstance`는 프리팹 인스턴스가 아닌 오브젝트에서 정상적으로 비어 있다.
  안 거르면 SampleScene 하나에서만 505건이 나온다(실제 유효 건수는 24건).
- **UnityEvent는 `m_Target`과 `m_MethodName`만 본다.** `m_Arguments.m_ObjectArgument`는 인자 타입이
  Object가 아니면 정상적으로 비어 있어 오탐이 된다.
- **빈 배열은 검사하지 않는다.** 넣어 봤더니 3개 씬에서 335건이 나왔고 전부 노이즈였다
  (세이브 상태 리스트, UnityEvent 내부 `m_Calls`). 배열의 "비어 있음"은 런타임
  `WiringGuard.RequireNotEmpty`가 담당한다.
- **프리팹 에셋은 검사 대상이 아니다.** 씬에 배치된 프리팹 *인스턴스*는 검사되지만, 런타임에
  생성되는 타워(`Data/TowerData/TowerPrefab/TP_*.prefab`) 같은 에셋은 이 도구가 보지 않는다.
- **`execute_script`로 넘기는 파일에 `using System;`을 넣지 말 것.** 컴파일이 실패하면 원인 대신
  판독 불가능한 Roslyn 리소스 에러만 돌아온다.
- 리포트는 `Temp/`(gitignore 대상)에 쓰이므로 커밋에 섞이지 않는다. Unity가 `Temp/`를 정리하면
  같이 사라지니, 남겨야 하면 다른 곳으로 복사한다.

## Troubleshooting

| 증상 | 원인 / 조치 |
|---|---|
| `[WiringChecker] 플레이 모드에서는 검사할 수 없습니다.` | 플레이를 정지하고 다시 실행 |
| `execute_script`가 `WiringCheckerRunner`를 못 찾음 | 에디터 어셈블리가 아직 컴파일 전. `AssetDatabase.Refresh()` 후 `check_compile_errors` |
| `[WiringOptional]`을 붙였는데 여전히 에러 | 같은 이름 필드를 가진 **다른 타입**에 붙였을 수 있다. 리포트의 경로(`UI_IngameWindow._resourceSlots...IconImage`)를 따라가 실제 선언 타입을 확인할 것 — `UI_IngameWindow.ResourceSlot`과 `ResourceAmountSlot`처럼 툴팁까지 같은 쌍둥이 타입이 실재한다 |
| Coplay 호출이 전부 타임아웃 | 에디터에 클릭 안 된 모달 다이얼로그가 떠 있는지 확인 |
