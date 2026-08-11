---
name: stringtable-diff
description: 로컬 스트링테이블 CSV(en_us/ko_kr)에서 새로 추가·변경된 키를 골라 구글 스프레드시트에 그대로 붙여넣을 TSV로 클립보드에 담는다. "언어파일 diff", "새로 추가한 키 시트에 옮기기", "스트링테이블 동기화", "번역 추가분 뽑기", localization/stringtable diff to clipboard, sync locale CSV to Google Sheets 요청에 사용.
---

# 스트링테이블 → 구글 시트 동기화

언어 원본은 **구글 스프레드시트**(`Id` / `en_us` / `ko_kr`)이고, 저장소의
`Assets/StreamingAssets/Localization/*.csv`는 임시 사본이다. Claude Code는 시트에 접근할 수 없어
로컬 CSV에만 키를 추가하므로, 작업이 끝나면 추가분을 시트로 옮겨야 한다.

이 스킬이 그 옮기는 작업을 대신한다. git diff로 **새로 생긴 키만** 뽑아
`Id<TAB>en_us<TAB>ko_kr` TSV로 클립보드에 넣는다. 사람은 시트에서 Ctrl+V만 하면 된다.

아래 경로는 모두 저장소 루트 기준이다. 스크립트는 자기 위치에서 저장소 루트를 찾으므로
**어느 디렉터리에서 실행해도 된다.**

## 사전 준비

없다. Windows PowerShell 5.1 + git만 있으면 된다. (확인: `5.1.26100.8875`)

## 실행 (기본 경로)

커밋 전, 방금 추가한 키를 뽑는다:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .claude\skills\stringtable-diff\stringtable-diff.ps1
```

`-ExecutionPolicy Bypass`는 빼지 말 것. 이 저장소 클론의 `LocalMachine` 정책은 `Undefined`(=기본
`Restricted`)라서, 팀원 PC에서 그냥 `.\stringtable-diff.ps1`로 실행하면 차단될 수 있다.

출력 예시 (실제 실행 결과):

```
저장소   : C:/Users/jokh9/Documents/TowerAndDragon
비교     : HEAD -> 워킹트리
언어     : en_us, ko_kr  (시트 열 순서)

신규 2개 / 변경 0개 / 삭제 0개
클립보드에 담은 행: 2개 (-Include added)
  * 번역 미확정 값 2개 포함 ([미정] / [TBD])

--- 미리보기 ---
  resource_forecast_baby_dragon_feed	[TBD] Baby dragon feed	새끼용 먹이
  resource_warning_slime	[TBD] Short by {0}. ...	{0} 부족합니다. ...

클립보드 완료. 시트 마지막 행 A열을 선택하고 Ctrl+V.
```

붙여넣을 행이 0개면 **클립보드를 건드리지 않는다.** (기존 복사 내용이 날아가지 않게)

## 주요 옵션

| 옵션 | 용도 |
|---|---|
| `-Since <ref>` | 기준 리비전. 기본 `HEAD`. 시트에 마지막으로 반영한 지점 이후 전부 뽑으려면 `-Since origin/master` |
| `-To <ref>` | 비교 대상. 기본은 워킹트리. 특정 커밋만 보려면 `-Since 0cae331^ -To 0cae331` |
| `-Include added\|changed\|all` | 기본 `added`(신규만). 값이 바뀐 키까지 필요하면 `all` |
| `-Header` | `Id / en_us / ko_kr` 헤더 행을 붙인다. **빈 시트를 처음 채울 때만** 쓸 것 |
| `-Languages en_us,ko_kr` | 시트 열 순서와 같아야 한다. 언어가 늘면 `-Languages en_us,ko_kr,ja_jp` |
| `-Out <경로>` | TSV를 파일로도 저장(UTF-8 BOM). 클립보드를 못 쓰는 원격 세션용 |
| `-Quiet` | 요약 없이 TSV만 표준 출력. 파이프로 넘길 때 |
| `-NoClipboard` | 클립보드를 건드리지 않는다 |

검증된 호출들:

```powershell
$S = ".claude\skills\stringtable-diff\stringtable-diff.ps1"

# 특정 커밋이 추가한 키
powershell -NoProfile -ExecutionPolicy Bypass -File $S -Since "0cae331^" -To 0cae331

# 신규 + 변경, 헤더까지
powershell -NoProfile -ExecutionPolicy Bypass -File $S -Include all -Header

# 클립보드 없이 파일로만
powershell -NoProfile -ExecutionPolicy Bypass -File $S -Out locale-add.tsv -NoClipboard
```

## 붙여넣은 뒤 확인할 것

스크립트가 요약에 경고로 찍어주는 항목들이다.

- **`! <lang> 누락`** — 한쪽 언어에만 있는 키. 시트에는 빈 셀로 들어가니 채워야 한다.
- **`! 값에 줄바꿈이 든 키`** — 값 안에 개행이 있는 행은 따옴표로 감싸 보낸다.
  시트가 이걸 한 셀로 받는지는 **붙여넣고 눈으로 확인**할 것. (`claim_overlay_produce`,
  `dragon_motherDragon_info_*`가 여기 해당한다. 시트 쪽 붙여넣기 동작은 이 환경에서 검증하지 못했다.)
- **`! 로컬에서 사라진 키`** — 삭제분은 붙여넣기에 들어가지 않는다. 시트에서 직접 지워야 한다.
- **`* 번역 미확정 값`** — `[미정]` / `[TBD]` 접두사가 붙은 값. 번역 확정 전까지 유지한다.

## 함정

- **신규/변경이 수백 개로 나오면 내 작업분이 아니다.** 누군가 시트 → 로컬 방향으로 전체
  동기화를 하면 `-Since HEAD` 기준으로 대규모 diff가 잡힌다(실제로 신규 175 / 변경 71 / 삭제 33이
  잡힌 적 있다). 이때 클립보드에 담긴 걸 그대로 시트에 붙이면 중복 행이 생긴다.
  내가 추가한 키만 필요하면 `-Since`를 동기화 이후 커밋으로 좁힐 것.

- **언어 CSV를 `git checkout --`로 되돌리지 말 것.** 로컬 CSV는 임시 사본이지만, 시트에서 막
  내려받은 미커밋 상태일 수 있다. 되돌리면 그 동기화 결과가 날아간다(시트에서 다시 내려받아야 한다).
  이 스킬은 CSV를 **읽기만** 하므로 스킬 자체는 안전하다.

- **스크립트는 UTF-8 BOM으로 저장해야 한다.** PowerShell 5.1은 BOM 없는 `.ps1`을 CP949로 읽어서
  한글 주석·문자열이 깨지고 `Missing closing '}'` 같은 파싱 에러로 죽는다. 편집 도구가 BOM을
  날렸다면 이걸로 되살린다 (검증됨):

  ```powershell
  $p = ".claude\skills\stringtable-diff\stringtable-diff.ps1"
  $t = [System.IO.File]::ReadAllText($p, (New-Object System.Text.UTF8Encoding $false))
  [System.IO.File]::WriteAllText($p, $t, (New-Object System.Text.UTF8Encoding $true))
  ```

- **`$변수` 바로 뒤에 한글을 붙이지 말 것.** 한글은 PowerShell 변수명에 쓸 수 있는 문자라서
  `"$count개"`는 `$count개`라는 없는 변수로 파싱된다. 반드시 `"$($count)개"`.

- **라인 단위 diff로 다시 만들지 말 것.** 값에 줄바꿈이 든 레코드가 실제로 6개 있어서
  (`claim_overlay_produce`, `dragon_motherDragon_info_life/fire/ice/time/stone`)
  `git diff`의 추가 라인을 긁으면 레코드가 반토막 난다. 리비전별로 CSV 전체를 파싱해 키로 비교한다.

- **개행 정규화 없이 비교하면 팬텀 diff가 난다.** 이 저장소는 `core.autocrlf=true`라서
  워킹트리 파일의 필드 내부 개행은 CRLF, `git show`가 주는 blob은 LF다. 정규화를 빼면
  워킹트리가 깨끗한데도 위 6개 키가 매번 "변경됨"으로 잡힌다. 스크립트가 파싱 시점에 LF로 통일한다.

- **git 출력은 콘솔 코드페이지를 탄다.** `git show`를 그냥 캡처하면 한글이 깨진다.
  `System.Diagnostics.Process`에 `StandardOutputEncoding = UTF8`을 직접 지정해야 한다.

- **PowerShell 5.1에는 `ProcessStartInfo.ArgumentList`가 없다** (.NET Framework).
  인자는 `Arguments` 문자열로 직접 조립해야 한다.

- **`switch`는 컬렉션을 풀어서 내보낸다.** `$ids = switch (...) { 'added' { $list } }`로 쓰면
  리스트가 비었을 때 `$null`, 1개일 때 스칼라가 되어 뒤의 `.Count`가 터진다. `@()`로 감쌀 것.

- **클립보드 TSV 끝에는 개행을 두지 않는다.** 의도적이다. 끝에 개행이 있으면 시트에 빈 행이
  하나 더 생긴다.

## 문제 해결

| 증상 | 원인 / 해결 |
|---|---|
| `Missing closing '}' in statement block` + 출력에 `濡쒖뺄` 같은 깨진 글자 | BOM이 날아갔다. 위 "함정" 첫 항목의 복구 명령 실행 |
| `The variable '$xxx개' cannot be retrieved` | `$var` 뒤에 한글이 붙었다. `$($var)개`로 고칠 것 |
| 워킹트리가 깨끗한데 `변경 N개`가 나온다 | 개행 정규화가 빠졌다. `ConvertFrom-StringTableCsv`의 LF 통일 코드 확인 |
| `The property 'Count' cannot be found on this object` | `switch` 언롤. `@()`로 감쌀 것 |
| `NativeCommandError` / stderr가 에러로 승격 | 네이티브 명령에 `2>&1`을 걸었다. PS 5.1에서는 붙이지 말 것 |
| `... 실행할 수 없습니다` / 실행 정책 차단 | `-ExecutionPolicy Bypass`를 빼먹었다 |
| `'<ref>'에 ...csv 없음 — 빈 테이블로 본다` 경고 | 그 리비전에 해당 언어 파일이 없다. 파일 도입 이전 커밋과 비교하면 정상 동작이며, 전 키가 신규로 잡힌다 |
| `Set-Clipboard를 쓸 수 없다` | 클립보드가 없는 환경. `-Out <경로>`로 파일에 받아서 옮길 것 |
