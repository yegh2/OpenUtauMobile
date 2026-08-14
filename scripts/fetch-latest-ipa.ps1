#Requires -Version 7.0
<#
.SYNOPSIS
    等待 GitHub Actions 构建完成，下载产出文件（unsigned IPA zip）并解压到桌面。

.DESCRIPTION
    用于 OpenUtauMobile dev 分支的 iOS 未签名 IPA 构建：
    1. 从 git 凭据管理器读取 GitHub token（无需手动输入）。
    2. 找到最新的 "Build Unsigned IPA" workflow run（可等待最新一次 push 触发的 run）。
    3. 轮询直到构建完成（成功/失败）。
    4. 下载 artifact zip 到桌面，并解压（zip 与 .ipa 都保留在桌面）。
    5. 构建失败时以非零退出码结束。

.PARAMETER Wait
    等待最新一次 in_progress 的 run 完成。默认 $true。
    若为 $false，仅下载最近一次已完成的成功构建产物。

.PARAMETER PollSeconds
    轮询间隔（秒）。默认 60。

.PARAMETER DesktopPath
    产出文件放置目录。默认使用系统桌面。

.EXAMPLE
    ./scripts/fetch-latest-ipa.ps1
#>
param(
    [bool]$Wait = $true,
    [int]$PollSeconds = 60,
    [string]$DesktopPath = ""
)

$ErrorActionPreference = 'Stop'
$Repo = 'yegh2/OpenUtauMobile'
$Branch = 'dev'
$WorkflowName = 'Build Unsigned IPA (OpenUtauMobile)'
$ArtifactName = 'OpenUtauMobile-unsigned-ipa'

if ([string]::IsNullOrWhiteSpace($DesktopPath)) {
    $DesktopPath = [Environment]::GetFolderPath('Desktop')
}
if (-not (Test-Path $DesktopPath)) {
    throw "桌面目录不存在: $DesktopPath"
}

# ── 从 git 凭据管理器读取 GitHub token ────────────────────────────────
function Get-GitHubToken {
    $cred = "protocol=https`nhost=github.com`n`n" | git credential fill 2>$null
    $line = ($cred | Select-String '^password=' | Select-Object -First 1)
    if ($null -eq $line) {
        throw '未能从 git 凭据管理器读取 GitHub token，请先执行 git push 登录。'
    }
    return $line.ToString().Replace('password=', '').Trim()
}

$Token = Get-GitHubToken
$Headers = @{
    Authorization = "Bearer $Token"
    'User-Agent'  = 'OpenUtauMobile-fetch-ipa'
}

# ── 查找 workflow run ─────────────────────────────────────────────────
function Get-LatestRun {
    $runs = Invoke-RestMethod -Uri "https://api.github.com/repos/$Repo/actions/runs?branch=$Branch&per_page=10" `
        -Headers $Headers -TimeoutSec 30
    return $runs.workflow_runs | Where-Object { $_.name -eq $WorkflowName } | Select-Object -First 1
}

$run = Get-LatestRun
if ($null -eq $run) {
    throw "未找到 workflow '$WorkflowName' 的 run（分支 $Branch）。"
}

Write-Host "run #$($run.id)  status=$($run.status)  conclusion=$($run.conclusion)"
Write-Host "  created: $($run.created_at)  head: $($run.head_sha.Substring(0, 7))"

# ── 等待构建完成 ──────────────────────────────────────────────────────
if ($Wait -and $run.status -ne 'completed') {
    Write-Host "等待构建完成（每 ${PollSeconds}s 轮询）..."
    while ($run.status -ne 'completed') {
        Start-Sleep -Seconds $PollSeconds
        $run = Invoke-RestMethod -Uri "https://api.github.com/repos/$Repo/actions/runs/$($run.id)" `
            -Headers $Headers -TimeoutSec 30
        Write-Host "  run #$($run.id) status=$($run.status) conclusion=$($run.conclusion) ($(Get-Date -Format 'HH:mm:ss'))"
    }
}

if ($run.conclusion -ne 'success') {
    Write-Error "构建未成功: conclusion=$($run.conclusion)"
    Write-Host "查看详情: https://github.com/$Repo/actions/runs/$($run.id)"
    exit 1
}

# ── 下载 artifact ─────────────────────────────────────────────────────
$artifacts = Invoke-RestMethod -Uri "https://api.github.com/repos/$Repo/actions/runs/$($run.id)/artifacts" `
    -Headers $Headers -TimeoutSec 30
$artifact = $artifacts.artifacts | Where-Object { $_.name -eq $ArtifactName } | Select-Object -First 1
if ($null -eq $artifact) {
    throw "run #$($run.id) 未找到 artifact '$ArtifactName'。可用: $($artifacts.artifacts.name -join ', ')"
}

$zipPath = Join-Path $DesktopPath "$($artifact.name).zip"
Write-Host "下载 artifact -> $zipPath"
Invoke-WebRequest -Uri "https://api.github.com/repos/$Repo/actions/artifacts/$($artifact.id)/zip" `
    -Headers $Headers -OutFile $zipPath -TimeoutSec 300

# ── 解压到桌面（zip 与内部 .ipa 都保留） ─────────────────────────────
Write-Host "解压 -> $DesktopPath"
Expand-Archive -Path $zipPath -DestinationPath $DesktopPath -Force

$extracted = Get-ChildItem -Path $DesktopPath -Filter '*.ipa' -File -ErrorAction SilentlyContinue |
    Where-Object { $_.LastWriteTimeUtc -ge $run.created_at }
foreach ($f in $extracted) {
    Write-Host "  [OK] $($f.FullName)"
}

if (-not $extracted) {
    Write-Warning '未在桌面找到新的 .ipa 文件，请检查 zip 内容。'
}

Write-Host "完成: $zipPath"
