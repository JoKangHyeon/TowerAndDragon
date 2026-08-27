#requires -version 5.1
<#
.SYNOPSIS
    팀원별 git 기여 사실(fact)을 집계해 마크다운 팩트시트로 뽑는다.

.DESCRIPTION
    이 스크립트는 "센 것"만 낸다. 누가 무엇을 설계했는지, 어떤 기능을 담당했는지 같은
    해석은 하지 않는다. 그 해석은 SKILL.md의 지시에 따라 Claude가 팩트시트를 읽고 쓴다.

    집계 방식:
      - 작성자는 이메일로 묶는다. 커밋에 박힌 이름은 깨졌거나(조강현) 유니코드 정규화가
        갈리거나(이하늘 NFD/NFC) 기기마다 다르므로(나상욱 3종) 신뢰하지 않는다.
      - .meta 파일은 전부 버린다. 실제 파일의 1:1 그림자라 두 배로 세는 것 뿐이다.
      - 라인 수가 아니라 "파일을 만진 횟수"로 센다. .prefab/.unity/.asset은 직렬화된
        YAML이라 인스펙터 값 하나만 바꿔도 수천 줄이 갈린다.
      - 머지 커밋은 내용 집계에서 뺀다(--no-merges). 대신 PR은 따로 훑어서, 머지를 누른
        사람이 아니라 그 안의 커밋을 실제로 쓴 사람에게 귀속시킨다.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File .claude\skills\contribution-report\contribution-facts.ps1

.EXAMPLE
    # 한 명만, 알파 마일스톤 이후만
    ... -File <스크립트> -Member 이하늘 -Since 2026-08-01
#>
[CmdletBinding()]
param(
    # 집계 시작 지점. 리비전(origin/master, 커밋해시) 또는 날짜(2026-08-01). 비우면 전체 이력.
    [string]$Since,

    # 집계 끝 지점. 기본 HEAD.
    [string]$To = 'HEAD',

    # 대상 팀원. 이름 일부 또는 이메일 일부로 부분 일치.
    # 비우면 `git config user.email` 로 이 PC의 사용자를 자동으로 찾는다.
    # (점유율 계산은 언제나 전원 기준으로 하고, 출력만 걸러낸다)
    [string]$Member,

    # 팀 전원을 출력한다. -Member 보다 우선한다.
    [switch]$AllMembers,

    # 팩트시트 저장 경로. 비우면 임시 폴더에 만든다.
    [string]$Out,

    # 팀원별로 보여줄 상위 디렉터리 개수.
    [int]$TopDirectories = 15,

    # 팀원별로 보여줄 단독 소유 파일 개수.
    [int]$TopFiles = 25,

    # 커밋 목록에 실을 최대 개수(팀원당). 0이면 전부.
    [int]$MaxCommitList = 0,

    # PR 귀속 분석을 건너뛴다. 머지 1건당 git 호출이 1회라 이력이 길면 느리다.
    [switch]$SkipPullRequests,

    # 진행 상황을 찍지 않는다.
    [switch]$Quiet
)

$ErrorActionPreference = 'Stop'

# ── 상수 ────────────────────────────────────────────────────────────────────

# 커밋 이메일 → 표시 이름. .mailmap 과 같은 내용을 담고 있으나 여기서 다시 선언한다.
# (git --author= 로 거르면 '150312623+smflsmfqh' 같은 값의 정규식 이스케이프에 발목이 잡히고,
#  mailmap 적용 여부가 집계 정확도를 좌우하게 된다. 이메일로 직접 버킷팅하는 편이 안전하다.)
$MEMBER_BY_EMAIL = [ordered]@{
    'garidana@gmail.com'                           = '나상욱'
    'jokh980324@naver.com'                         = '조강현'
    'skyee46@gmail.com'                            = '이하늘'
    '150312623+smflsmfqh@users.noreply.github.com' = '이하늘'
    'bmj1644035@gmail.com'                         = '김지해'
}

# CLAUDE.md 의 마일스톤. 연도는 첫 커밋 연도를 따라간다.
# EndMonthDay 가 $null 인 구간이 마지막이며 그 이후 전부를 흡수한다.
$MILESTONE_BOUNDARIES = @(
    [pscustomobject]@{ Name = '프로토타입'; EndMonthDay = '07-31' }
    [pscustomobject]@{ Name = '알파';       EndMonthDay = '08-14' }
    [pscustomobject]@{ Name = '베타';       EndMonthDay = '08-28' }
    [pscustomobject]@{ Name = '제출';       EndMonthDay = $null }
)

$CATEGORY_ORDER = @('스크립트', '데이터', '씬·프리팹', '아트·연출', '문서', '기타')

$CATEGORY_BY_EXTENSION = @{
    'cs' = '스크립트'; 'shader' = '스크립트'; 'asmdef' = '스크립트'
    'cginc' = '스크립트'; 'hlsl' = '스크립트'; 'ps1' = '스크립트'

    'asset' = '데이터'; 'csv' = '데이터'; 'json' = '데이터'; 'txt' = '데이터'
    'preset' = '데이터'; 'inputactions' = '데이터'

    'unity' = '씬·프리팹'; 'prefab' = '씬·프리팹'

    'png' = '아트·연출'; 'jpg' = '아트·연출'; 'jpeg' = '아트·연출'; 'tga' = '아트·연출'
    'psd' = '아트·연출'; 'mat' = '아트·연출'; 'anim' = '아트·연출'; 'controller' = '아트·연출'
    'overridecontroller' = '아트·연출'; 'fbx' = '아트·연출'; 'obj' = '아트·연출'
    'wav' = '아트·연출'; 'mp3' = '아트·연출'; 'ogg' = '아트·연출'
    'ttf' = '아트·연출'; 'otf' = '아트·연출'; 'fontsettings' = '아트·연출'
    'spriteatlas' = '아트·연출'; 'physicmaterial' = '아트·연출'; 'terrainlayer' = '아트·연출'

    'md' = '문서'; 'mermaid' = '문서'; 'svg' = '문서'; 'html' = '문서'; 'pdf' = '문서'
}

$OTHER_CATEGORY   = '기타'
$IGNORED_EXTENSION = 'meta'
$DIRECTORY_DEPTH   = 3      # Assets/Scripts/UI 까지
$DEFAULT_OUT_NAME  = 'towerdragon-contribution-facts.md'

$RECORD_SEPARATOR = [char]1
$FIELD_SEPARATOR  = [char]2

# 마크다운 코드스팬용 백틱. 문자열 리터럴로 직접 쓰면 PowerShell 이 이스케이프 문자로 먹는다.
# 특히 "$( ... "`x`" ... )" 처럼 부분식 안에 중첩되면 파서가 통째로 깨진다.
$BT = [string][char]0x60

# ── git 호출 ────────────────────────────────────────────────────────────────

# git 출력은 콘솔 코드페이지를 타서 그냥 캡처하면 한글이 깨진다.
# StandardOutputEncoding 을 직접 UTF8 로 박아야 한다. (stringtable-diff 스킬과 같은 함정)
# PowerShell 5.1(.NET Framework)에는 ProcessStartInfo.ArgumentList 가 없어 인자는 문자열로 조립한다.
$script:RepoRoot = $null

function Invoke-Git {
    param(
        [Parameter(Mandatory)][string]$Arguments,
        [switch]$AllowFailure
    )

    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName               = 'git'
    $psi.Arguments              = $Arguments
    $psi.WorkingDirectory       = if ($script:RepoRoot) { $script:RepoRoot } else { $PSScriptRoot }
    $psi.UseShellExecute        = $false
    $psi.CreateNoWindow         = $true
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError  = $true
    $psi.StandardOutputEncoding = [System.Text.Encoding]::UTF8
    $psi.StandardErrorEncoding  = [System.Text.Encoding]::UTF8

    $process = [System.Diagnostics.Process]::Start($psi)
    $stdout  = $process.StandardOutput.ReadToEnd()
    $stderr  = $process.StandardError.ReadToEnd()
    $process.WaitForExit()

    if ($process.ExitCode -ne 0) {
        if ($AllowFailure) { return $null }
        throw "git $Arguments 실패 (exit $($process.ExitCode)): $stderr"
    }
    return $stdout
}

function Write-Progressly {
    param([string]$Message)
    if (-not $Quiet) { Write-Host $Message }
}

# ── 도우미 ──────────────────────────────────────────────────────────────────

function Get-Category {
    param([string]$Path)
    $extension = ''
    $lastDot = $Path.LastIndexOf('.')
    if ($lastDot -ge 0) { $extension = $Path.Substring($lastDot + 1).ToLowerInvariant() }
    if ($CATEGORY_BY_EXTENSION.ContainsKey($extension)) { return $CATEGORY_BY_EXTENSION[$extension] }
    return $OTHER_CATEGORY
}

function Get-DirectoryKey {
    param([string]$Path)
    $segments = @($Path -split '/')
    if ($segments.Count -le 1) { return '(저장소 루트)' }
    $keep = [Math]::Min($DIRECTORY_DEPTH, $segments.Count - 1)
    return ($segments[0..($keep - 1)] -join '/')
}

function Add-Count {
    param([hashtable]$Table, [string]$Key, [int]$Amount = 1)
    if (-not $Table.ContainsKey($Key)) { $Table[$Key] = 0 }
    $Table[$Key] += $Amount
}

function Format-Percent {
    param([double]$Numerator, [double]$Denominator)
    if ($Denominator -le 0) { return '-' }
    return ('{0:N1}%' -f (($Numerator / $Denominator) * 100))
}

function Get-MilestoneName {
    param([datetime]$Date, [int]$Year)
    foreach ($boundary in $MILESTONE_BOUNDARIES) {
        if ($null -eq $boundary.EndMonthDay) { return $boundary.Name }
        $end = [datetime]::ParseExact("$Year-$($boundary.EndMonthDay)", 'yyyy-MM-dd', $null)
        if ($Date -le $end) { return $boundary.Name }
    }
    return $MILESTONE_BOUNDARIES[-1].Name
}

# ── 준비 ────────────────────────────────────────────────────────────────────

$topLevel = Invoke-Git '-c core.quotepath=false rev-parse --show-toplevel'
if ([string]::IsNullOrWhiteSpace($topLevel)) { throw 'git 저장소를 찾지 못했다.' }
$script:RepoRoot = $topLevel.Trim()

if ([string]::IsNullOrWhiteSpace($Out)) {
    $Out = Join-Path $env:TEMP $DEFAULT_OUT_NAME
}

$range = if ([string]::IsNullOrWhiteSpace($Since)) { $To } else { "$Since..$To" }
$rangeLabel = if ([string]::IsNullOrWhiteSpace($Since)) { "전체 이력 → $To" } else { "$Since → $To" }

Write-Progressly "저장소 : $script:RepoRoot"
Write-Progressly "범위   : $rangeLabel"

$headHash = (Invoke-Git "rev-parse --short $To").Trim()

# ── 1. 커밋 수집 ────────────────────────────────────────────────────────────

Write-Progressly '커밋 수집 중...'

$logFormat = "%x01%H%x02%h%x02%ae%x02%ad%x02%s"
$rawLog = Invoke-Git "-c core.quotepath=false log --no-merges --date=short --format=$logFormat --name-only $range"

$commits          = New-Object System.Collections.ArrayList
$memberByHash     = @{}
$unmappedEmails   = @{}
$fileAuthors      = @{}   # 파일 → 그 파일을 만진 팀원 이름 집합
$fileTouchByMember = @{}  # 팀원 → (파일 → 터치 수)
$dirTouchByMember  = @{}  # 팀원 → (디렉터리 → 터치 수)
$dirTouchTotal     = @{}  # 디렉터리 → 전체 터치 수
$catCountByMember  = @{}  # 팀원 → (카테고리 → 터치 수)

$records = @($rawLog -split $RECORD_SEPARATOR | Where-Object { $_.Trim() -ne '' })

foreach ($record in $records) {
    $lines = @($record -split "`r?`n")
    $header = @($lines[0] -split $FIELD_SEPARATOR)
    if ($header.Count -lt 5) { continue }

    $fullHash = $header[0]
    $shortHash = $header[1]
    $email = $header[2].ToLowerInvariant()
    $dateText = $header[3]
    $subject = $header[4]

    $memberName = $null
    if ($MEMBER_BY_EMAIL.Contains($email)) {
        $memberName = $MEMBER_BY_EMAIL[$email]
    }
    else {
        Add-Count $unmappedEmails $email
        continue
    }

    $memberByHash[$fullHash] = $memberName

    # 파일 목록: 헤더 다음 줄부터. .meta 와 빈 줄은 버린다.
    $files = New-Object System.Collections.ArrayList
    for ($i = 1; $i -lt $lines.Count; $i++) {
        $path = $lines[$i].Trim()
        if ($path -eq '') { continue }
        # quotepath=false 로도 특수문자가 있으면 따옴표로 감싸 나온다.
        if ($path.StartsWith('"') -and $path.EndsWith('"') -and $path.Length -gt 1) {
            $path = $path.Substring(1, $path.Length - 2)
        }
        if ($path.ToLowerInvariant().EndsWith(".$IGNORED_EXTENSION")) { continue }
        [void]$files.Add($path)
    }

    if (-not $fileTouchByMember.ContainsKey($memberName)) { $fileTouchByMember[$memberName] = @{} }
    if (-not $dirTouchByMember.ContainsKey($memberName))  { $dirTouchByMember[$memberName]  = @{} }
    if (-not $catCountByMember.ContainsKey($memberName))  { $catCountByMember[$memberName]  = @{} }

    $dirsInCommit = @{}
    foreach ($path in $files) {
        if (-not $fileAuthors.ContainsKey($path)) { $fileAuthors[$path] = New-Object System.Collections.Generic.HashSet[string] }
        [void]$fileAuthors[$path].Add($memberName)

        Add-Count $fileTouchByMember[$memberName] $path
        Add-Count $catCountByMember[$memberName] (Get-Category $path)

        $dirKey = Get-DirectoryKey $path
        Add-Count $dirTouchByMember[$memberName] $dirKey
        Add-Count $dirTouchTotal $dirKey
        Add-Count $dirsInCommit $dirKey
    }

    # 이 커밋이 주로 건드린 영역 (커밋 목록에 한 줄로 붙일 라벨)
    $primaryDirs = @($dirsInCommit.GetEnumerator() | Sort-Object -Property Value -Descending | Select-Object -First 2 | ForEach-Object { $_.Key })

    [void]$commits.Add([pscustomobject]@{
        FullHash  = $fullHash
        ShortHash = $shortHash
        Member    = $memberName
        Date      = [datetime]::ParseExact($dateText, 'yyyy-MM-dd', $null)
        DateText  = $dateText
        Subject   = $subject
        FileCount = $files.Count
        Areas     = $primaryDirs
    })
}

if ($commits.Count -eq 0) { throw "범위 '$range' 에 집계할 커밋이 없다." }

$sortedCommits = @($commits | Sort-Object -Property Date)
$firstYear = $sortedCommits[0].Date.Year

foreach ($commit in $sortedCommits) {
    $commit | Add-Member -NotePropertyName Milestone -NotePropertyValue (Get-MilestoneName -Date $commit.Date -Year $firstYear)
}

Write-Progressly "  커밋 $($sortedCommits.Count)개 / 팀원 $($fileTouchByMember.Keys.Count)명"

# ── 2. PR 귀속 ──────────────────────────────────────────────────────────────

$pullRequestsByMember = @{}
$mergedByMember = @{}   # 누가 PR 을 머지했는가 = 통합·리뷰 담당 신호
$unattributedPullRequests = New-Object System.Collections.ArrayList
$mergeCount = 0
$pullRequestCount = 0

if (-not $SkipPullRequests) {
    $mergeFormat = "%x01%H%x02%h%x02%ae%x02%ad%x02%s%x02%P"
    $rawMerges = Invoke-Git "-c core.quotepath=false log --merges --date=short --format=$mergeFormat $range"
    $mergeRecords = @($rawMerges -split $RECORD_SEPARATOR | Where-Object { $_.Trim() -ne '' })
    $mergeCount = $mergeRecords.Count

    Write-Progressly "머지 $($mergeCount)건 분석 중 (건너뛰려면 -SkipPullRequests)..."

    $processed = 0
    foreach ($record in $mergeRecords) {
        $processed++
        if (-not $Quiet -and ($processed % 50) -eq 0) {
            Write-Progressly "  $($processed)/$($mergeCount)"
        }

        $fields = @(($record -split "`r?`n")[0] -split $FIELD_SEPARATOR)
        if ($fields.Count -lt 6) { continue }

        $mergerEmail  = $fields[2].ToLowerInvariant()
        $mergeDate    = $fields[3]
        $mergeSubject = $fields[4]
        $parents = @($fields[5].Trim() -split '\s+' | Where-Object { $_ -ne '' })
        if ($parents.Count -lt 2) { continue }

        # "Merge pull request #258 from JoKangHyeon/beta"
        #
        # PR 머지만 센다. 로컬 동기화 머지(git pull 이 만드는 "Merge branch 'master' of ...",
        # "Merge remote-tracking branch 'origin/X' into X")는 기능 단위가 아니라 그냥 따라잡기이고,
        # 그 안에는 남이 쓴 커밋이 통째로 들어 있다. 그대로 세면 pull 한 번으로 남의 커밋
        # 수십 개가 자기 기여로 둔갑한다. (실제로 42개짜리가 나왔다.)
        $match = [regex]::Match($mergeSubject, 'Merge pull request #(\d+) from (\S+)')
        if (-not $match.Success) { continue }

        $prNumber = $match.Groups[1].Value
        # from 뒤는 "<GitHub 계정>/<브랜치>" 형태다. 계정은 저장소 소유자라 늘 같으므로 떼어낸다.
        $branch = $match.Groups[2].Value -replace '^[^/]+/', ''
        $pullRequestCount++

        # 머지를 누른 사람. 코드 기여와는 별개의 축(통합·리뷰 담당)이라 따로 센다.
        if ($MEMBER_BY_EMAIL.Contains($mergerEmail)) {
            Add-Count $mergedByMember $MEMBER_BY_EMAIL[$mergerEmail]
        }

        # 머지가 실제로 들여온 커밋들 = 첫 부모에는 없고 두 번째 부모에는 있는 것들.
        $broughtIn = Invoke-Git "rev-list --no-merges $($parents[0])..$($parents[1])" -AllowFailure
        if ($null -eq $broughtIn) { continue }

        $authorTally = @{}
        foreach ($hash in @($broughtIn -split "`r?`n" | Where-Object { $_.Trim() -ne '' })) {
            $key = $hash.Trim()
            if ($memberByHash.ContainsKey($key)) { Add-Count $authorTally $memberByHash[$key] }
        }

        $entry = [pscustomobject]@{
            Number       = [int]$prNumber
            Branch       = $branch
            Date         = $mergeDate
            CommitCount  = 0
            Contributors = ''
        }

        if ($authorTally.Keys.Count -eq 0) {
            [void]$unattributedPullRequests.Add($entry)
            continue
        }

        $ranked = @($authorTally.GetEnumerator() | Sort-Object -Property Value -Descending)
        $entry.CommitCount = ($ranked | Measure-Object -Property Value -Sum).Sum
        $entry.Contributors = (($ranked | ForEach-Object { "$($_.Key) $($_.Value)" }) -join ', ')

        $owner = $ranked[0].Key
        if (-not $pullRequestsByMember.ContainsKey($owner)) { $pullRequestsByMember[$owner] = New-Object System.Collections.ArrayList }
        [void]$pullRequestsByMember[$owner].Add($entry)
    }
}

# ── 3. 단독 소유 파일 ───────────────────────────────────────────────────────

$exclusiveByMember = @{}
foreach ($path in $fileAuthors.Keys) {
    if ($fileAuthors[$path].Count -ne 1) { continue }
    $owner = @($fileAuthors[$path])[0]
    if (-not $exclusiveByMember.ContainsKey($owner)) { $exclusiveByMember[$owner] = New-Object System.Collections.ArrayList }
    [void]$exclusiveByMember[$owner].Add([pscustomobject]@{
        Path    = $path
        Touches = $fileTouchByMember[$owner][$path]
    })
}

# ── 4. 팩트시트 작성 ────────────────────────────────────────────────────────

# 이름을 $allMembers 로 하면 안 된다. PowerShell 은 변수명 대소문자를 구분하지 않아
# [switch]$AllMembers 파라미터와 같은 변수가 되고, 배열을 넣는 순간 형변환 에러로 죽는다.
$knownMembers = @($MEMBER_BY_EMAIL.Values | Select-Object -Unique | Where-Object { $fileTouchByMember.ContainsKey($_) })

# 기본은 "이 스킬을 쓴 사람 한 명". 팀 전체가 필요하면 -AllMembers 를 명시한다.
$identitySource = ''
$targetMembers = $knownMembers

if (-not $AllMembers) {
    $needle = $Member
    if ([string]::IsNullOrWhiteSpace($needle)) {
        $configuredEmail = (Invoke-Git 'config user.email' -AllowFailure)
        if ($null -eq $configuredEmail -or [string]::IsNullOrWhiteSpace($configuredEmail)) {
            throw 'git config user.email 이 비어 있다. -Member 로 대상을 직접 지정하거나 -AllMembers 를 쓸 것.'
        }
        $needle = $configuredEmail.Trim()
        $identitySource = "git config user.email = $needle"
    }

    $needle = $needle.Trim()
    $emailMatches = @($MEMBER_BY_EMAIL.Keys | Where-Object { $_ -like "*$needle*" } | ForEach-Object { $MEMBER_BY_EMAIL[$_] })
    $targetMembers = @($knownMembers | Where-Object { $_ -like "*$needle*" -or $emailMatches -contains $_ })

    if ($targetMembers.Count -eq 0) {
        $hint = if ($identitySource -ne '') { "이 PC 의 git 신원($needle)이 팀원 명단에 없다." } else { "'$Member' 에 해당하는 팀원이 없다." }
        throw "$hint 가능한 값: $($knownMembers -join ', '). 명단에 없는 이메일이면 .mailmap 과 스크립트의 `$MEMBER_BY_EMAIL 양쪽에 추가할 것."
    }
}

$isSingleMember = ($targetMembers.Count -eq 1)

$milestoneNames = @($MILESTONE_BOUNDARIES | ForEach-Object { $_.Name })

# 이름을 $out 으로 하면 안 된다. PowerShell 은 변수명 대소문자를 구분하지 않아
# [string]$Out 파라미터와 같은 변수가 되고, StringBuilder 가 조용히 문자열로 캐스팅된다.
$report = New-Object System.Text.StringBuilder

function Add-Line {
    param([string]$Text = '')
    [void]$report.AppendLine($Text)
}

function Format-Code {
    param([string]$Text)
    return $BT + $Text + $BT
}

function Join-Code {
    param([object[]]$Items, [string]$Separator = ', ')
    return (@($Items | ForEach-Object { $BT + $_ + $BT }) -join $Separator)
}

$titleSuffix = if ($isSingleMember) { $targetMembers[0] } else { Split-Path $script:RepoRoot -Leaf }
Add-Line "# 기여도 팩트시트 — $titleSuffix"
Add-Line
Add-Line "| 항목 | 값 |"
Add-Line "|---|---|"
if ($identitySource -ne '') {
    Add-Line "| 대상 | **$($targetMembers[0])** (이 PC 의 $identitySource) |"
}
elseif ($isSingleMember) {
    Add-Line "| 대상 | **$($targetMembers[0])** |"
}
$mergeNote = if ($SkipPullRequests) { ' — PR 분석 건너뜀' } else { '' }
Add-Line "| 집계 범위 | $rangeLabel (끝 = $(Format-Code $headHash)) |"
Add-Line "| 저장소 전체 커밋 | $($sortedCommits.Count)개 (머지 제외) |"
Add-Line "| 머지 커밋 | $($mergeCount)개$mergeNote |"
if (-not $SkipPullRequests) {
    Add-Line "| 그중 GitHub PR 머지 | $($pullRequestCount)건 (나머지는 로컬 동기화 머지라 제외) |"
}
Add-Line "| 기간 | $($sortedCommits[0].DateText) ~ $($sortedCommits[-1].DateText) |"
Add-Line "| 집계 단위 | 파일 터치 수 (.meta 제외 / 라인 수 아님) |"
if ($unmappedEmails.Keys.Count -eq 0) {
    Add-Line "| 매핑 실패 | **없음 — 모든 커밋이 팀원에게 귀속됨** |"
}
else {
    $unmappedText = (($unmappedEmails.GetEnumerator() | Sort-Object -Property Value -Descending | ForEach-Object { "$($_.Key)($($_.Value))" }) -join ', ')
    Add-Line "| 매핑 실패 | **$unmappedText — 스크립트의 $(Format-Code '$MEMBER_BY_EMAIL') 에 추가할 것** |"
}
Add-Line
Add-Line '> 이 문서는 스크립트가 센 사실만 담는다. 역할·담당·설계 의도 같은 해석은 들어 있지 않다.'
Add-Line
if ($isSingleMember) {
    Add-Line '> 점유율은 팀 전원을 집계한 뒤 이 사람 몫만 뽑은 값이라, 한 명만 뽑아도 비교 수치는 정확하다.'
    Add-Line
}

$summaryHeading = if ($isSingleMember) { '## 요약' } else { '## 전원 요약' }
Add-Line $summaryHeading
Add-Line
Add-Line ("| 팀원 | 커밋 | 비중 | 담당 PR | 머지 수행 | 첫 커밋 | 마지막 커밋 | " + ($CATEGORY_ORDER -join ' | ') + " |")
Add-Line ("|---|---:|---:|---:|---:|---|---|" + (($CATEGORY_ORDER | ForEach-Object { '---:' }) -join '|') + "|")

foreach ($name in $targetMembers) {
    $mine = @($sortedCommits | Where-Object { $_.Member -eq $name })
    $cells = foreach ($category in $CATEGORY_ORDER) {
        $value = 0
        if ($catCountByMember[$name].ContainsKey($category)) { $value = $catCountByMember[$name][$category] }
        $value
    }
    $ownedPrs = if ($pullRequestsByMember.ContainsKey($name)) { $pullRequestsByMember[$name].Count } else { 0 }
    $merged   = if ($mergedByMember.ContainsKey($name)) { $mergedByMember[$name] } else { 0 }
    $prText     = if ($SkipPullRequests) { '-' } else { "$ownedPrs" }
    $mergedText = if ($SkipPullRequests) { '-' } else { "$merged" }
    Add-Line ("| $name | $($mine.Count) | $(Format-Percent $mine.Count $sortedCommits.Count) | $prText | $mergedText | $($mine[0].DateText) | $($mine[-1].DateText) | " + ($cells -join ' | ') + " |")
}
Add-Line
Add-Line '- **비중** — 저장소 전체 커밋 대비. **커밋 수는 기여량이 아니다.** 데이터 에셋을 다루면 늘고 스크립트를 깊게 파면 줄어든다.'
Add-Line '- **담당 PR** — 그 PR 안의 커밋을 가장 많이 쓴 사람. 코드 기여의 축이다.'
Add-Line '- **머지 수행** — PR 머지 버튼을 누른 사람. 통합·리뷰 담당의 축이며, 코드 기여와 별개다.'
Add-Line '  두 숫자가 크게 어긋나도 오류가 아니라 역할 차이다.'
Add-Line '- 숫자 열(스크립트~기타)은 커밋 수가 아니라 **파일을 만진 횟수**다. 같은 파일을 열 번 고치면 10이다.'
Add-Line

# 마일스톤별 커밋
Add-Line '## 마일스톤별 커밋 수'
Add-Line
Add-Line ("| 팀원 | " + ($milestoneNames -join ' | ') + " |")
Add-Line ("|---|" + (($milestoneNames | ForEach-Object { '---:' }) -join '|') + "|")
foreach ($name in $targetMembers) {
    $cells = foreach ($milestone in $milestoneNames) {
        @($sortedCommits | Where-Object { $_.Member -eq $name -and $_.Milestone -eq $milestone }).Count
    }
    Add-Line ("| $name | " + ($cells -join ' | ') + " |")
}
Add-Line
# 한글은 PowerShell 변수명에 쓸 수 있는 문자다. "$firstYear년" 은 없는 변수로 파싱되어 빈 값이 된다.
Add-Line "마일스톤 경계는 CLAUDE.md 기준: 프로토타입 ~7/31 · 알파 ~8/14 · 베타 ~8/28 · 제출 그 이후 ($($firstYear)년 기준)"
Add-Line

# 팀원별 상세
foreach ($name in $targetMembers) {
    $mine = @($sortedCommits | Where-Object { $_.Member -eq $name })
    $myEmails = @($MEMBER_BY_EMAIL.Keys | Where-Object { $MEMBER_BY_EMAIL[$_] -eq $name })

    if (-not $isSingleMember) {
        Add-Line '---'
        Add-Line
        Add-Line "# $name"
        Add-Line
        $emailText = Join-Code $myEmails
        Add-Line "커밋 $($mine.Count)개 · $($mine[0].DateText) ~ $($mine[-1].DateText) · 이메일 $emailText"
        Add-Line
    }

    Add-Line '## 가장 많이 만진 디렉터리'
    Add-Line
    Add-Line '| 디렉터리 | 이 팀원 터치 | 이 팀원 작업 중 | 그 디렉터리 전체 중 |'
    Add-Line '|---|---:|---:|---:|'
    $myTotalTouches = ($dirTouchByMember[$name].Values | Measure-Object -Sum).Sum
    $topDirs = @($dirTouchByMember[$name].GetEnumerator() | Sort-Object -Property Value -Descending | Select-Object -First $TopDirectories)
    foreach ($dir in $topDirs) {
        $shareOfDir = Format-Percent $dir.Value $dirTouchTotal[$dir.Key]
        Add-Line "| $(Format-Code $dir.Key) | $($dir.Value) | $(Format-Percent $dir.Value $myTotalTouches) | **$shareOfDir** |"
    }
    Add-Line
    Add-Line '마지막 열이 높을수록 그 영역을 혼자 맡았다는 뜻이다. 낮으면 공동 작업 구역이다.'
    Add-Line

    Add-Line '## 이 팀원만 만진 파일 (단독 소유)'
    Add-Line
    if ($exclusiveByMember.ContainsKey($name)) {
        $exclusive = @($exclusiveByMember[$name] | Sort-Object -Property Touches -Descending)
        Add-Line "전체 $($exclusive.Count)개 중 상위 $([Math]::Min($TopFiles, $exclusive.Count))개."
        Add-Line
        Add-Line '| 파일 | 수정 횟수 |'
        Add-Line '|---|---:|'
        foreach ($file in @($exclusive | Select-Object -First $TopFiles)) {
            Add-Line "| $(Format-Code $file.Path) | $($file.Touches) |"
        }
    }
    else {
        Add-Line '없음.'
    }
    Add-Line

    Add-Line '## 병합된 PR·브랜치 (이 팀원 커밋이 가장 많은 것)'
    Add-Line
    $myMergeCount = if ($mergedByMember.ContainsKey($name)) { $mergedByMember[$name] } else { 0 }
    if (-not $SkipPullRequests -and $myMergeCount -gt 0) {
        Add-Line "이 팀원이 직접 머지한 PR: **$($myMergeCount)건** (전체 $($pullRequestCount)건 중 $(Format-Percent $myMergeCount $pullRequestCount))"
        Add-Line
    }
    if ($SkipPullRequests) {
        Add-Line '`-SkipPullRequests` 로 건너뛰었다.'
    }
    elseif ($pullRequestsByMember.ContainsKey($name)) {
        $prs = @($pullRequestsByMember[$name] | Sort-Object -Property Number)
        Add-Line "$($prs.Count)건. 머지를 누른 사람이 아니라 **안에 담긴 커밋의 작성자**로 귀속했다."
        Add-Line '로컬 동기화 머지는 제외하고 GitHub PR 머지만 센다.'
        Add-Line
        Add-Line '| PR | 브랜치 | 머지일 | 담긴 커밋 |'
        Add-Line '|---|---|---|---|'
        foreach ($pr in $prs) {
            Add-Line "| #$($pr.Number) | $(Format-Code $pr.Branch) | $($pr.Date) | $($pr.Contributors) |"
        }
    }
    else {
        Add-Line '커밋 최다 작성자로 잡힌 PR은 없다.'
        if ($myMergeCount -gt 0) {
            Add-Line '위 머지 건수를 볼 것 — 이 팀원은 남의 PR을 받아 통합하는 쪽에 서 있었다.'
        }
    }
    Add-Line

    Add-Line '## 커밋 전체 (시간순)'
    Add-Line
    $listed = $mine
    if ($MaxCommitList -gt 0 -and $mine.Count -gt $MaxCommitList) {
        $listed = @($mine | Select-Object -Last $MaxCommitList)
        Add-Line "**$($mine.Count)개 중 최근 $($MaxCommitList)개만 싣는다 (-MaxCommitList).** 앞쪽 $($mine.Count - $MaxCommitList)개는 잘렸다."
        Add-Line
    }
    foreach ($milestone in $milestoneNames) {
        $inMilestone = @($listed | Where-Object { $_.Milestone -eq $milestone })
        if ($inMilestone.Count -eq 0) { continue }
        Add-Line "### $milestone ($($inMilestone.Count)개)"
        Add-Line
        foreach ($commit in $inMilestone) {
            $areaText = ''
            if ($commit.Areas.Count -gt 0) { $areaText = ' — ' + (Join-Code $commit.Areas) }
            Add-Line "- $(Format-Code $commit.ShortHash) $($commit.DateText) $($commit.Subject)$areaText"
        }
        Add-Line
    }
}

if ($unattributedPullRequests.Count -gt 0) {
    Add-Line '---'
    Add-Line
    Add-Line '## 귀속하지 못한 PR'
    Add-Line
    Add-Line '들여온 커밋이 집계 범위 밖이라 작성자를 셀 수 없었다. `-Since` 를 좁혀 쓸 때 주로 생긴다.'
    Add-Line
    Add-Line '| PR | 브랜치 | 머지일 |'
    Add-Line '|---|---|---|'
    foreach ($pr in @($unattributedPullRequests | Sort-Object -Property Number)) {
        Add-Line "| #$($pr.Number) | $(Format-Code $pr.Branch) | $($pr.Date) |"
    }
}

# UTF-8 BOM 으로 저장한다. BOM 없이 쓰면 다른 도구가 CP949 로 읽어 한글이 깨진다.
$outDirectory = Split-Path -Parent $Out
if ($outDirectory -and -not (Test-Path $outDirectory)) {
    [void](New-Item -ItemType Directory -Path $outDirectory -Force)
}
[System.IO.File]::WriteAllText($Out, $report.ToString(), (New-Object System.Text.UTF8Encoding $true))

Write-Progressly ''
Write-Progressly "팩트시트 : $Out"
if ($identitySource -ne '') {
    Write-Progressly "대상     : $($targetMembers -join ', ')  (이 PC 의 git 신원으로 자동 판별)"
}
else {
    Write-Progressly "대상     : $($targetMembers -join ', ')"
}
if ($unmappedEmails.Keys.Count -gt 0) {
    Write-Progressly "경고     : 매핑 안 된 이메일 $($unmappedEmails.Keys.Count)개 — 그만큼 커밋이 누락됐다"
}

# 파이프로 넘길 수 있게 경로를 반환한다.
$Out
