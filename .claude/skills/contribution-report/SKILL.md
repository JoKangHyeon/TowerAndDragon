---
name: contribution-report
description: git 이력에서 이 PC 사용자 본인의 기여를 찾아 정리한 마크다운 문서를 Docs/Contributions/ 에 만든다. 발표자료의 "팀원 역할·기여" 슬라이드에 쓸 근거를 커밋 해시·PR 번호와 함께 뽑는다. "기여도 정리", "내가 뭐 했는지 정리해줘", "내 기여 정리", "발표자료용 기여 정리", "내가 맡은 파트 정리", contribution report, summarize my commits, my contribution breakdown 요청에 사용.
---

# 내 기여도 정리

git 이력에서 **이 PC 사용자 본인의 기여**를 뽑아 `Docs/Contributions/<이름>.md`로 만든다.
팀원 각자가 자기 PC에서 한 번씩 돌리면 발표자료용 기여 문서가 사람 수만큼 모인다.

대상은 `git config user.email`로 자동 판별한다. 물어보지 말 것.

## 두 단계로 나뉜다

| 단계 | 담당 | 산출물 |
|---|---|---|
| 1. 집계 | `contribution-facts.ps1` | 팩트시트 (센 것만. 해석 없음) |
| 2. 서술 | Claude | `Docs/Contributions/<이름>.md` |

이 문서는 기업협약 과제의 최종 발표자료에 들어가고 **실명 평가에 연결된다.**
경로 몇 개 겹쳤다고 "이 사람이 점령 시스템을 설계했다"라고 쓰면 나중에 분쟁이 난다.

> **작성 계약 — 기능·역할에 대한 모든 주장에는 근거를 단다.**
> 커밋 해시(`de3aa84`) 또는 PR 번호(`#258`)를 문장에 함께 적는다.
> 근거를 댈 수 없으면 그 문장은 쓰지 않는다.

## 실행

### 1. 팩트시트 뽑기

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .claude\skills\contribution-report\contribution-facts.ps1
```

`-ExecutionPolicy Bypass`를 빼지 말 것. 팀원 PC의 `LocalMachine` 정책이 `Undefined`(=기본
`Restricted`)라 그냥 실행하면 차단된다.

전체 이력(커밋 1115개 / 머지 199건)에 **약 25초** 걸린다. 머지 1건당 git 호출이 1회라 그렇다.

```
저장소 : C:/Users/garid/Documents/Gtihub/TowerAndDragon
범위   : 전체 이력 → HEAD
커밋 수집 중...
  커밋 916개 / 팀원 4명
머지 199건 분석 중 (건너뛰려면 -SkipPullRequests)...
팩트시트 : C:\Users\...\Temp\towerdragon-contribution-facts.md
대상     : 나상욱  (이 PC 의 git 신원으로 자동 판별)
```

마지막 두 줄에서 **경로와 대상**을 확인한다. 대상이 예상과 다르면 그 PC의 git 설정이
다른 것이니 멈추고 사용자에게 알린다.

### 2. 팩트시트를 읽고 문서 쓰기

팩트시트를 Read하고 `Docs/Contributions/<이름>.md`를 쓴다. 파일명은 팩트시트 머리말의
`대상` 칸에 있는 이름을 그대로 쓴다 (`나상욱.md`, `이하늘.md`).

**이미 그 파일이 있으면 덮어쓰기 전에 사용자에게 확인한다.** 손으로 고친 내용이 있을 수 있다.

## 출력 문서 형식

```markdown
# <이름> — 기여 정리

집계: 전체 이력 (2026-07-03 ~ 2026-08-27) · 본인 커밋 381개 · 담당 PR 48건
생성: `.claude/skills/contribution-report`

## 담당 파트

한두 문장. 디렉터리 점유율이 근거다.

## 주요 기여

- **<기능 이름>** (`a1b2c3d`, #45) — 무엇을 만들었는지 한두 문장
- ...

## 작업 영역

| 디렉터리 | 점유율 |
|---|---:|

## 시기별 흐름

- **프로토타입 (N커밋)** — ...
- **알파 (N커밋)** — ...
- **베타 (N커밋)** — ...
```

작성 규칙:

- 항목마다 **커밋 해시나 PR 번호**를 붙인다. 근거 없는 문장은 쓰지 않는다.
- 기능 이름은 지어내지 말고 **커밋 제목과 브랜치 이름에서 가져온다**
  (`feature/229-ResearchUI` → 연구 UI). 커밋 제목이 이미 한국어라 그대로 쓸 수 있다.
- 시기 구분은 팩트시트의 마일스톤(프로토타입/알파/베타/제출)을 그대로 쓴다.
  CLAUDE.md의 마일스톤과 같은 경계라서 발표자료 일정 슬라이드와 맞는다.
- **다른 팀원과 비교하지 않는다.** 이 문서는 본인 것만 다룬다. "가장 많이 기여했다" 같은
  순위 표현도 쓰지 않는다.
- 팩트시트의 `매핑 실패` 칸이 "없음"이 아니면 그 사실을 문서에 적는다. 커밋이 누락됐다는 뜻이다.

## 팩트시트 읽는 법

**축이 두 개다. 하나만 보면 본인 기여를 잘못 적는다.**

| 열 | 뜻 |
|---|---|
| 담당 PR | 그 PR 안의 커밋을 가장 많이 쓴 사람. **코드 기여**의 축 |
| 머지 수행 | PR 머지 버튼을 누른 사람. **통합·리뷰**의 축 |

담당 PR이 0인데 커밋이 많으면 **오류가 아니다.** master에 직접 커밋했거나 통합 담당인
경우다(이 저장소에는 실제로 그런 사례가 있다 — 커밋 169개 중 157개가 master 직접 커밋이라
담당 PR이 0으로 잡힌다). 이럴 때는 머지 수행 건수와 커밋 목록으로 기여를 서술한다.

그 외:

- **커밋 수 ≠ 기여량.** 데이터 에셋(`.asset`)을 다루면 커밋이 부풀고, 스크립트를 깊게
  파면 커밋이 적다.
- **숫자 열은 파일 터치 수다.** 라인 수가 아니다. `.prefab`/`.unity`/`.asset`은 직렬화된
  YAML이라 인스펙터 값 하나만 바꿔도 수천 줄이 갈려서 라인 수는 의미가 없다.
- **"그 디렉터리 전체 중" 열이 담당 파트의 가장 강한 신호다.** 70%를 넘으면 사실상 전담,
  30% 아래면 공동 작업 구역이라 "전담했다"라고 쓰면 안 된다.
- **단독 소유 파일 표는 보조 자료로만 쓴다.** 밸런스용 `.asset`이 상위를 도배하는 경우가
  있어 그것만 보면 파트를 착각한다. 디렉터리 표와 커밋 제목을 함께 볼 것.
- **`Docs/<이름>/`은 개인 작업 노트, `CoplayScripts/`는 Coplay MCP 일회성 에디터 스크립트다.**
  기여로 세되 게임 기능으로 서술하지 않는다.

## 옵션

기본값으로 충분하다. 아래는 필요할 때만.

| 옵션 | 용도 |
|---|---|
| `-Member <이름\|이메일>` | 본인이 아닌 다른 팀원을 뽑는다. 부분 일치 |
| `-AllMembers` | 팀 전원을 한 파일에 뽑는다 |
| `-Since <ref\|날짜>` | 집계 시작. 기본은 전체 이력 |
| `-To <ref>` | 집계 끝. 기본 `HEAD` |
| `-Out <경로>` | 팩트시트 저장 위치. 기본은 임시 폴더 |
| `-MaxCommitList <n>` | 커밋 목록 개수 제한(최근 n개). 0이면 전부. 기본 0 |
| `-TopDirectories <n>` | 디렉터리 표 행 수. 기본 15 |
| `-TopFiles <n>` | 단독 소유 파일 표 행 수. 기본 25 |
| `-SkipPullRequests` | PR 분석 생략. 25초 → 3초. **담당 PR·머지 수행이 전부 빠진다** |
| `-Quiet` | 진행 표시 없이 경로만 출력 |

```powershell
$S = ".claude\skills\contribution-report\contribution-facts.ps1"

# 기본 — 이 PC 사용자 본인
powershell -NoProfile -ExecutionPolicy Bypass -File $S

# 다른 팀원
powershell -NoProfile -ExecutionPolicy Bypass -File $S -Member 이하늘

# 팀 전원 한 파일에
powershell -NoProfile -ExecutionPolicy Bypass -File $S -AllMembers

# 빠르게 훑기 (PR 정보 없음)
powershell -NoProfile -ExecutionPolicy Bypass -File $S -SkipPullRequests -MaxCommitList 20
```

## 신원 통합 — 이 스킬이 성립하는 이유

`git shortlog -sn`을 그냥 돌리면 **4명이 10명으로 쪼개져 나온다.** 원인이 셋이고 전부
커밋 객체에 이미 박혀 있어 되돌릴 수 없다.

| 사람 | 커밋에 남은 형태 | 합계 |
|---|---|---:|
| 나상욱 | `Sangwook Na` 343 + `나상욱` 37 + `SangMacBook` 33 | 413 |
| 조강현 | `ì¡JoKangHyeon` 209 + `조강현` 86 | 295 |
| 이하늘 | `이하늘`(NFD) 187 + `이하늘`(NFC) 44 + GitHub noreply 9 | 240 |
| 김지해 | `KJH` 163 + `김지해` 4 | 167 |

- 조강현의 이름은 **커밋에 깨진 채로 저장돼 있다.** UTF-8 `조`(EC A1 B0)의 마지막 바이트가
  유실돼 `ì¡`가 됐다.
- 이하늘은 **같은 글자가 NFD(자모 분리)와 NFC 두 형태로 존재한다.** macOS와 Windows의
  유니코드 정규화 차이다. 눈으로는 구별되지 않는다.

합계 413+295+240+167 = **1115 = 전체 커밋 수**. 잔여 0건이라 누구의 작업도 누락되지 않는다.

이 통합은 두 곳에 있고 **둘 다 이메일만으로 키를 잡는다.**

- 저장소 루트 `.mailmap` — 사람이 `git shortlog -sne` / `git blame`을 돌릴 때
- 스크립트의 `$MEMBER_BY_EMAIL` — 집계할 때, 그리고 **본인 자동 판별**에 쓴다

깨진 이름이나 NFD 문자열을 코드에 적으려 하지 말 것.
팀원이 늘거나 새 이메일로 커밋하면 두 곳 모두에 추가한다. 빠뜨리면 두 가지가 일어난다 —
그 사람이 스킬을 돌렸을 때 "git 신원이 팀원 명단에 없다"로 죽고, 남이 돌린 팩트시트
머리말의 `매핑 실패` 칸에 그 이메일이 커밋 수와 함께 찍힌다.

## 함정

- **머지 커밋을 기여로 세면 안 된다.** `git pull`이 만드는 `Merge branch 'master' of ...`
  안에는 남이 쓴 커밋이 통째로 들어 있다. 그대로 세면 pull 한 번으로 남의 커밋 수십 개가
  자기 기여로 둔갑한다(실제로 42개짜리가 나왔다). 스크립트는 내용 집계에서 머지를 빼고
  (`--no-merges`), PR은 `Merge pull request #N` 형태만 센다. 머지 199건 중 PR은 101건이다.

- **PR 브랜치의 `from` 뒤 계정 이름은 작성자가 아니다.** 이 저장소는 전부
  `JoKangHyeon/feature/...`로 나오지만 저장소 소유자 계정일 뿐이고, 안의 커밋은 다른
  사람이 쓴 경우가 대부분이다. 스크립트는 계정 부분을 떼고 **내부 커밋 작성자**로 귀속한다.

- **`-Since`로 범위를 좁히면 "단독 소유" 표를 믿을 수 없다.** 단독 소유는 *그 범위 안에서*
  혼자 만졌다는 뜻이라, 7월에 남이 만든 파일을 8월에 나만 건드렸으면 내 단독 소유로 잡힌다.
  범위를 좁혀 뽑을 때는 단독 소유 표로 담당 파트를 주장하지 말고 디렉터리 점유율만 쓴다.

- **`.meta`를 세면 전부 두 배가 된다.** 실제 파일의 1:1 그림자다. 스크립트가 버린다.

- **스크립트는 UTF-8 BOM으로 저장해야 한다.** PowerShell 5.1은 BOM 없는 `.ps1`을 CP949로
  읽어 한글 주석·문자열이 깨지고 `Missing closing '}'` 같은 파싱 에러로 죽는다.
  편집 도구가 BOM을 날렸다면 이걸로 되살린다:

  ```powershell
  $p = ".claude\skills\contribution-report\contribution-facts.ps1"
  $t = [System.IO.File]::ReadAllText($p, (New-Object System.Text.UTF8Encoding $false))
  [System.IO.File]::WriteAllText($p, $t, (New-Object System.Text.UTF8Encoding $true))
  ```

- **`$변수` 바로 뒤에 한글을 붙이지 말 것.** 한글은 PowerShell 변수명에 쓸 수 있는 문자라
  `"$year년"`은 `$year년`이라는 없는 변수로 파싱돼 **조용히 빈 값이 된다.** 에러도 안 난다.
  반드시 `"$($year)년"`.

- **파라미터와 같은 이름의 지역 변수를 만들지 말 것.** PowerShell은 변수명 대소문자를
  구분하지 않는다. 이 스크립트에서 두 번 당했다 — `$out`은 `[string]$Out`과 충돌해
  StringBuilder가 조용히 문자열로 캐스팅됐고, `$allMembers`는 `[switch]$AllMembers`와
  충돌해 형변환 에러로 죽었다. 지금은 `$report` / `$knownMembers`를 쓴다.

- **마크다운 백틱을 문자열 리터럴로 쓰지 말 것.** PowerShell의 이스케이프 문자다.
  특히 `"$( ... "`x`" ... )"`처럼 부분식 안에 중첩되면 파서가 통째로 깨진다.
  스크립트는 `$BT` 상수와 `Format-Code` / `Join-Code` 헬퍼를 쓴다.

- **git 출력은 콘솔 코드페이지를 탄다.** `System.Diagnostics.Process`에
  `StandardOutputEncoding = UTF8`을 직접 지정해야 한글이 안 깨진다. PowerShell 5.1에는
  `ProcessStartInfo.ArgumentList`가 없어 인자는 문자열로 조립해야 한다.
  (`stringtable-diff` 스킬이 같은 함정을 문서화하고 있다.)

- **한글 경로는 따옴표로 감싸 나온다.** `-c core.quotepath=false`로 막고, 그래도 남는
  따옴표는 파싱할 때 벗긴다.

## 문제 해결

| 증상 | 원인 / 해결 |
|---|---|
| `이 PC 의 git 신원(...)이 팀원 명단에 없다` | 그 이메일이 `$MEMBER_BY_EMAIL`에 없다. `.mailmap`과 스크립트 양쪽에 추가하거나, 임시로 `-Member <이름>`을 쓴다 |
| `git config user.email 이 비어 있다` | git 신원 미설정. `git config --global user.email "..."` 또는 `-Member` 사용 |
| 대상이 예상과 다른 사람으로 잡힌다 | 그 PC의 git 설정이 다른 사람 것이다. `git config user.email`로 확인 |
| `Missing closing '}' in statement block` + 출력에 `濡쒖뺄` 같은 깨진 글자 | BOM이 날아갔다. 위 복구 명령 실행 |
| `... 실행할 수 없습니다` / 실행 정책 차단 | `-ExecutionPolicy Bypass`를 빼먹었다 |
| 팩트시트 `매핑 실패` 칸에 이메일이 찍힌다 | 새 팀원·새 이메일. `.mailmap`과 `$MEMBER_BY_EMAIL` 양쪽에 추가 |
| 담당 PR이 0인데 커밋은 많다 | 오류가 아니다. master 직접 커밋이거나 통합 담당이다. 머지 수행 열과 커밋 목록으로 서술할 것 |
| `-Since`를 줬더니 `귀속하지 못한 PR`이 잔뜩 나온다 | PR이 들여온 커밋이 범위 밖이다. 정상 동작이며 `-Since`를 넓히면 사라진다 |
| `제출` 마일스톤이 0개다 | 마지막 커밋이 8/28 이전이면 정상이다 |
| 실행이 25초 넘게 걸린다 | 머지 1건당 git 호출 1회다. `-SkipPullRequests`로 건너뛸 수 있다 |
