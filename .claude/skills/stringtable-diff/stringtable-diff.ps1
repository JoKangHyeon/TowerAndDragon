<#
.SYNOPSIS
    로컬 스트링테이블 CSV의 신규/변경 키를 구글 스프레드시트 붙여넣기용 TSV로 뽑아 클립보드에 담는다.

.DESCRIPTION
    스트링테이블의 원본은 구글 스프레드시트(Id / en_us / ko_kr)이고, 로컬 CSV는 임시 사본이다.
    Claude Code 등 로컬 작업으로 CSV에만 키를 추가한 뒤, 이 스크립트로 추가분을 뽑아
    시트에 그대로 붙여넣어 원본을 동기화한다.

    라인 단위 diff를 쓰지 않는다. 값에 줄바꿈이 든 레코드가 있어서
    (예: dragon_motherDragon_info_life) 리비전별로 CSV 전체를 파싱해 키 단위로 비교한다.

.EXAMPLE
    # 커밋 전 작업분(HEAD -> 워킹트리)에서 새로 생긴 키
    .\stringtable-diff.ps1

.EXAMPLE
    # 시트에 마지막으로 반영한 지점 이후 추가분 전부
    .\stringtable-diff.ps1 -Since origin/master

.EXAMPLE
    # 특정 커밋이 추가한 키만
    .\stringtable-diff.ps1 -Since 0cae331^ -To 0cae331

.EXAMPLE
    # 신규 + 값이 바뀐 키까지, 헤더 포함
    .\stringtable-diff.ps1 -Include all -Header
#>
[CmdletBinding()]
param(
    # 기준 리비전. 이 시점에 없던 키를 "신규"로 본다.
    [string]$Since = 'HEAD',

    # 비교 대상 리비전. 비워두면 워킹트리의 실제 파일을 읽는다.
    [string]$To = '',

    # added: 신규 키만 / changed: 값이 바뀐 키만 / all: 신규 다음에 변경
    [ValidateSet('added', 'changed', 'all')]
    [string]$Include = 'added',

    # 시트의 열 순서와 같아야 한다. <LocalizationDir>/<lang>.csv 를 읽는다.
    [string[]]$Languages = @('en_us', 'ko_kr'),

    # 저장소 루트 기준 상대 경로.
    [string]$LocalizationDir = 'Assets/StreamingAssets/Localization',

    # 'Id  en_us  ko_kr' 헤더 행을 앞에 붙인다. 빈 시트를 처음 채울 때만 쓴다.
    [switch]$Header,

    # TSV를 파일로도 저장한다. 클립보드를 못 쓰는 환경(원격 세션 등)의 탈출구.
    [string]$Out = '',

    [switch]$NoClipboard,

    # 요약을 생략하고 TSV만 표준 출력으로 내보낸다. 파이프로 넘길 때 사용.
    [switch]$Quiet
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$TAB = "`t"
$PLACEHOLDER_PATTERNS = @('[미정]', '[TBD]')

# git 출력을 UTF-8로 직접 디코딩한다. PowerShell 기본 네이티브 출력 캡처는
# 콘솔 코드페이지를 타서 한글이 깨진다.
function Invoke-GitText {
    param(
        [string]$Repo,
        [string[]]$GitArgs,
        [switch]$AllowFailure
    )

    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = 'git'
    # .NET Framework(PowerShell 5.1)에는 ArgumentList가 없어서 문자열로 만든다.
    $psi.Arguments = ($GitArgs | ForEach-Object {
        if ($_ -match '[\s"]') { '"' + ($_ -replace '"', '\"') + '"' } else { $_ }
    }) -join ' '
    $psi.WorkingDirectory = $Repo
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true
    $utf8 = New-Object System.Text.UTF8Encoding $false
    $psi.StandardOutputEncoding = $utf8
    $psi.StandardErrorEncoding = $utf8

    $proc = [System.Diagnostics.Process]::Start($psi)
    $stdout = $proc.StandardOutput.ReadToEnd()
    $stderr = $proc.StandardError.ReadToEnd()
    $proc.WaitForExit()

    if ($proc.ExitCode -ne 0 -and -not $AllowFailure) {
        throw "git $($psi.Arguments) 실패 (exit $($proc.ExitCode)): $stderr"
    }

    [pscustomobject]@{
        ExitCode = $proc.ExitCode
        Text     = $stdout
        Error    = $stderr
    }
}

function Get-RepoRoot {
    param([string]$StartDir)

    $result = Invoke-GitText -Repo $StartDir -GitArgs @('rev-parse', '--show-toplevel')
    return $result.Text.Trim()
}

# CSV 텍스트 -> @{ Ids = 순서 보존 키 목록; Map = 키/값 }
# ConvertFrom-Csv에 전체 텍스트를 넘기면 인용된 줄바꿈을 제대로 처리한다.
function ConvertFrom-StringTableCsv {
    param(
        [AllowEmptyString()][string]$Text,
        [string]$Label
    )

    $ids = New-Object System.Collections.Generic.List[string]
    $map = @{}

    if ([string]::IsNullOrWhiteSpace($Text)) {
        return [pscustomobject]@{ Ids = $ids; Map = $map }
    }

    $records = $Text | ConvertFrom-Csv
    foreach ($record in $records) {
        $id = $record.Id
        if ([string]::IsNullOrEmpty($id)) { continue }

        if ($map.ContainsKey($id)) {
            Write-Warning "$Label : 키 중복 '$id' — 첫 값만 쓴다."
            continue
        }

        $value = $record.String
        if ($null -eq $value) { $value = '' }

        # 값 안의 개행을 LF로 통일한다.
        # core.autocrlf=true면 워킹트리 파일은 CRLF, git show가 주는 blob은 LF라서
        # 정규화하지 않으면 값에 줄바꿈이 든 레코드가 전부 "변경됨"으로 잡힌다.
        $value = $value -replace "`r`n", "`n" -replace "`r", "`n"

        $map[$id] = $value
        $ids.Add($id)
    }

    return [pscustomobject]@{ Ids = $ids; Map = $map }
}

# 리비전(또는 워킹트리)에서 언어 CSV 하나를 읽는다.
# 그 리비전에 파일이 없으면 빈 테이블로 취급한다(언어 신규 추가 시나리오).
function Read-StringTable {
    param(
        [string]$Repo,
        [string]$RelPath,
        [string]$Rev,
        [string]$Label
    )

    if ([string]::IsNullOrEmpty($Rev)) {
        $fullPath = Join-Path $Repo $RelPath
        if (-not (Test-Path -LiteralPath $fullPath)) {
            throw "언어 파일을 찾을 수 없다: $fullPath"
        }
        $text = [System.IO.File]::ReadAllText($fullPath, (New-Object System.Text.UTF8Encoding $false))
    }
    else {
        $show = Invoke-GitText -Repo $Repo -GitArgs @('show', "${Rev}:${RelPath}") -AllowFailure
        if ($show.ExitCode -ne 0) {
            Write-Warning "$Label : '$Rev'에 $RelPath 없음 — 빈 테이블로 본다."
            $text = ''
        }
        else {
            $text = $show.Text
        }
    }

    return ConvertFrom-StringTableCsv -Text $text -Label $Label
}

# 구글 시트에 붙여넣을 수 있게 필드를 감싼다.
# 탭/줄바꿈/따옴표가 있으면 인용하고 내부 따옴표는 겹쳐 쓴다. 시트는 이 규칙으로 파싱한다.
function Format-SheetField {
    param([AllowEmptyString()][string]$Value)

    if ($null -eq $Value) { return '' }

    if ($Value -match '[\t"]' -or $Value -match "`n" -or $Value -match "`r") {
        return '"' + ($Value -replace '"', '""') + '"'
    }

    return $Value
}

function Test-HasPlaceholder {
    param([AllowEmptyString()][string]$Value)

    foreach ($pattern in $PLACEHOLDER_PATTERNS) {
        if ($Value -and $Value.Contains($pattern)) { return $true }
    }

    return $false
}

# ---------------------------------------------------------------- 본체

$repoRoot = Get-RepoRoot -StartDir $PSScriptRoot
if ([string]::IsNullOrEmpty($repoRoot)) {
    throw 'git 저장소 루트를 찾지 못했다. 저장소 안에서 실행해야 한다.'
}

$targetLabel = if ([string]::IsNullOrEmpty($To)) { '워킹트리' } else { $To }

$baseline = [ordered]@{}
$target = [ordered]@{}
foreach ($lang in $Languages) {
    $relPath = "$LocalizationDir/$lang.csv"
    $baseline[$lang] = Read-StringTable -Repo $repoRoot -RelPath $relPath -Rev $Since -Label "$lang@$Since"
    $target[$lang] = Read-StringTable -Repo $repoRoot -RelPath $relPath -Rev $To -Label "$lang@$targetLabel"
}

# 키 순서는 -Languages 첫 언어의 파일 순서를 따르고, 그 언어에 없는 키는 뒤에 붙인다.
# 시트 행 순서가 파일 순서와 맞아야 나중에 눈으로 대조하기 쉽다.
$orderedIds = New-Object System.Collections.Generic.List[string]
$seen = New-Object System.Collections.Generic.HashSet[string]
foreach ($lang in $Languages) {
    foreach ($id in $target[$lang].Ids) {
        if ($seen.Add($id)) { $orderedIds.Add($id) }
    }
}

$baselineIds = New-Object System.Collections.Generic.HashSet[string]
foreach ($lang in $Languages) {
    foreach ($id in $baseline[$lang].Ids) { [void]$baselineIds.Add($id) }
}

$addedIds = New-Object System.Collections.Generic.List[string]
$changedIds = New-Object System.Collections.Generic.List[string]
foreach ($id in $orderedIds) {
    if (-not $baselineIds.Contains($id)) {
        $addedIds.Add($id)
        continue
    }

    $isChanged = $false
    foreach ($lang in $Languages) {
        $before = if ($baseline[$lang].Map.ContainsKey($id)) { $baseline[$lang].Map[$id] } else { '' }
        $after = if ($target[$lang].Map.ContainsKey($id)) { $target[$lang].Map[$id] } else { '' }
        if ($before -ne $after) { $isChanged = $true; break }
    }

    if ($isChanged) { $changedIds.Add($id) }
}

$targetIds = New-Object System.Collections.Generic.HashSet[string]
foreach ($id in $orderedIds) { [void]$targetIds.Add($id) }
$removedIds = New-Object System.Collections.Generic.List[string]
foreach ($lang in $Languages) {
    foreach ($id in $baseline[$lang].Ids) {
        if (-not $targetIds.Contains($id) -and $removedIds -notcontains $id) { $removedIds.Add($id) }
    }
}

# @()로 감싸야 한다. switch가 List를 파이프라인에 풀어써서
# 비면 $null, 1개면 스칼라가 되고 그 뒤 .Count가 깨진다.
$payloadIds = @()
switch ($Include) {
    'added' { $payloadIds = @($addedIds) }
    'changed' { $payloadIds = @($changedIds) }
    'all' { $payloadIds = @($addedIds) + @($changedIds) }
}

$lines = New-Object System.Collections.Generic.List[string]
if ($Header) {
    $lines.Add((@('Id') + $Languages) -join $TAB)
}

$missingByLang = @{}
foreach ($lang in $Languages) { $missingByLang[$lang] = New-Object System.Collections.Generic.List[string] }
$placeholderCount = 0
$multilineIds = New-Object System.Collections.Generic.List[string]

foreach ($id in $payloadIds) {
    $fields = New-Object System.Collections.Generic.List[string]
    $fields.Add((Format-SheetField -Value $id))

    foreach ($lang in $Languages) {
        $value = if ($target[$lang].Map.ContainsKey($id)) { $target[$lang].Map[$id] } else { '' }
        if (-not $target[$lang].Map.ContainsKey($id)) { $missingByLang[$lang].Add($id) }
        if (Test-HasPlaceholder -Value $value) { $placeholderCount++ }
        if ($value -match "`n" -and $multilineIds -notcontains $id) { $multilineIds.Add($id) }
        $fields.Add((Format-SheetField -Value $value))
    }

    $lines.Add(($fields -join $TAB))
}

# 시트에 붙여넣을 때 마지막 빈 행이 생기지 않게 CRLF로 잇고 끝에 개선을 두지 않는다.
$tsv = $lines -join "`r`n"

if ($Quiet) {
    if ($tsv) { Write-Output $tsv }
}
else {
    Write-Host "저장소   : $repoRoot"
    Write-Host "비교     : $Since -> $targetLabel"
    Write-Host "언어     : $($Languages -join ', ')  (시트 열 순서)"
    Write-Host ''
    Write-Host "신규 $($addedIds.Count)개 / 변경 $($changedIds.Count)개 / 삭제 $($removedIds.Count)개"
    Write-Host "클립보드에 담은 행: $($payloadIds.Count)개 (-Include $Include)"

    if ($placeholderCount -gt 0) {
        # 한글은 변수명에 쓸 수 있는 문자다. $var 뒤에 바로 한글이 붙으면 변수명으로 먹히니 반드시 $()로 감싼다.
        Write-Host "  * 번역 미확정 값 $($placeholderCount)개 포함 ($($PLACEHOLDER_PATTERNS -join ' / '))"
    }

    foreach ($lang in $Languages) {
        if ($missingByLang[$lang].Count -gt 0) {
            Write-Host "  ! $lang 누락 $($missingByLang[$lang].Count)개: $($missingByLang[$lang] -join ', ')"
        }
    }

    if ($removedIds.Count -gt 0) {
        Write-Host "  ! 로컬에서 사라진 키(붙여넣기에는 안 들어간다): $($removedIds -join ', ')"
    }

    if ($multilineIds.Count -gt 0) {
        # 값 안에 줄바꿈이 있는 행은 따옴표로 감싸 보낸다. 시트가 이걸 한 셀로 받는지
        # 붙여넣은 뒤 반드시 눈으로 확인해야 한다.
        Write-Host "  ! 값에 줄바꿈이 든 키 $($multilineIds.Count)개: $($multilineIds -join ', ')"
        Write-Host "    붙여넣은 뒤 이 행들이 한 셀에 들어갔는지 확인할 것."
    }

    if ($payloadIds.Count -gt 0) {
        Write-Host ''
        Write-Host '--- 미리보기 ---'
        $preview = $lines | Select-Object -First 6
        foreach ($line in $preview) {
            Write-Host ('  ' + ($line -replace "`r?`n", '<개행>'))
        }
        if ($lines.Count -gt 6) { Write-Host "  ... (총 $($lines.Count)행)" }
    }
}

if ($Out) {
    $outPath = if ([System.IO.Path]::IsPathRooted($Out)) { $Out } else { Join-Path (Get-Location).Path $Out }
    # 시트/엑셀이 한글을 바로 읽도록 BOM 있는 UTF-8로 저장한다.
    [System.IO.File]::WriteAllText($outPath, $tsv, (New-Object System.Text.UTF8Encoding $true))
    if (-not $Quiet) { Write-Host "파일 저장: $outPath" }
}

if (-not $NoClipboard) {
    if ($payloadIds.Count -eq 0) {
        if (-not $Quiet) { Write-Host '붙여넣을 행이 없어 클립보드는 건드리지 않았다.' }
    }
    elseif (Get-Command Set-Clipboard -ErrorAction SilentlyContinue) {
        Set-Clipboard -Value $tsv
        if (-not $Quiet) {
            Write-Host ''
            Write-Host "클립보드 완료. 시트 마지막 행 A열을 선택하고 Ctrl+V."
        }
    }
    else {
        Write-Warning 'Set-Clipboard를 쓸 수 없다. -Out 으로 파일에 저장해서 옮겨라.'
    }
}

exit 0
