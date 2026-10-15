# ============================================================================
#  ISUP 门禁接入 Web API —— 启停脚本
#
#  用法：
#    .\run.ps1                     启动（产物不存在时自动构建）
#    .\run.ps1 -Stop               停止
#    .\run.ps1 -Status             查看状态（端口 / 健康 / SDK 状态）
#    .\run.ps1 -Restart            重启
#    .\run.ps1 -Build              启动前强制构建
#    .\run.ps1 -Config Release
#    .\run.ps1 -SdkDir D:\isupsdk  手动指定 ISUP SDK 原生库目录
# ============================================================================
[CmdletBinding()]
param(
    [switch]$Stop,
    [switch]$Status,
    [switch]$Restart,
    [switch]$Build,
    [ValidateSet('Debug','Release')][string]$Config = 'Debug',
    [string]$SdkDir = '',
    [int]$WaitSeconds = 15
)

$ErrorActionPreference = 'Stop'

$RepoRoot   = $PSScriptRoot
$ProjectDir = Join-Path $RepoRoot 'src\ISUPSDK.AccessControl.WebApi'
$Solution   = Join-Path $RepoRoot 'ISUPSDK.AccessControl.sln'
$RunDir     = Join-Path $RepoRoot 'run'
$OutLog     = Join-Path $RunDir 'api.out.log'
$ErrLog     = Join-Path $RunDir 'api.err.log'
$ExePath    = Join-Path $ProjectDir ('bin\x64\' + $Config + '\net8.0-windows\ISUPSDK.AccessControl.WebApi.exe')
$BaseUrl    = 'http://127.0.0.1:5190'

function Get-ApiProcess {
    @(Get-Process -Name 'ISUPSDK.AccessControl.WebApi' -ErrorAction SilentlyContinue)
}

function Get-Health {
    try { Invoke-RestMethod ($BaseUrl + '/health') -TimeoutSec 5 } catch { $null }
}

function Show-Status {
    $procs = Get-ApiProcess
    if ($procs.Count -eq 0) { Write-Host '[status] not running' -ForegroundColor Yellow; return }
    Write-Host ('[status] running  PID=' + ($procs.Id -join ',') + '  since ' + $procs[0].StartTime) -ForegroundColor Green

    # 5190=API，其余为 SDK 监听端口：设备注册 / 报警 UDP / 报警 TCP / 图片服务
    foreach ($port in 5190,7660,7332,7333,8081) {
        $conn = Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue
        $mark = '-'
        if ($conn) { $mark = 'LISTENING' }
        Write-Host ('        port ' + $port + ' ' + $mark)
    }

    $h = Get-Health
    if ($h) {
        Write-Host ('        health: ' + $h.status + ' | sdkInitialized=' + $h.sdk.sdkInitialized + ' | online=' + $h.onlineDevices)
        Write-Host ('        register: ' + $h.sdk.settingsServerAddress + ':' + $h.sdk.settingsInitPort + ' | listenHandle=' + $h.sdk.listenHandle)
        Write-Host ('        alarms: count=' + $h.sdk.alarmCount + ' lastAt=' + $h.sdk.lastAlarmAt)
        if ($h.sdk.nativeDirectory) {
            Write-Host ('        native  : ' + $h.sdk.nativeDirectory)
        } else {
            Write-Host '        native  : NOT FOUND (HCISUPCMS.dll 等未定位到)' -ForegroundColor Yellow
        }
        if ($h.sdk.sdkInitialized -ne $true) {
            Write-Host '        -> SDK 未初始化（degraded）：检查 ISUP:NativeDirectory / ISUP_SDK_DIR 与端口占用' -ForegroundColor Yellow
        }
    } else {
        Write-Host '        health: no response (still starting?)' -ForegroundColor Yellow
    }

    Write-Host ('        swagger: ' + $BaseUrl + '/swagger')
    Write-Host ('        log    : ' + $OutLog)
}

function Stop-Api {
    $procs = Get-ApiProcess
    if ($procs.Count -eq 0) { Write-Host '[stop] not running' -ForegroundColor Yellow; return }
    $procs | Stop-Process -Force
    Start-Sleep -Seconds 2
    Write-Host ('[stop] killed PID=' + ($procs.Id -join ',')) -ForegroundColor Green
}

# 定位 ISUP SDK 原生库目录：需要一个同时含 HCISUPCMS.dll / HCISUPAlarm.dll 的目录。
function Resolve-SdkDir {
    if ($SdkDir) { return (Resolve-Path $SdkDir).Path }

    foreach ($variable in 'ISUP_SDK_DIR','HCISUPSDK_DIR') {
        $value = [Environment]::GetEnvironmentVariable($variable)
        if ($value -and (Test-Path (Join-Path $value 'HCISUPCMS.dll'))) { return $value }
    }

    $dir = Get-Item $RepoRoot
    while ($dir) {
        $candidates = @($dir.FullName)
        $candidates += Get-ChildItem -Path $dir.FullName -Directory -Filter 'ISUPSDK*' -ErrorAction SilentlyContinue |
            Select-Object -ExpandProperty FullName
        $candidates += Get-ChildItem -Path $dir.FullName -Directory -Filter 'HCISUPSDK*' -ErrorAction SilentlyContinue |
            Select-Object -ExpandProperty FullName
        foreach ($candidate in $candidates) {
            if (Test-Path (Join-Path $candidate 'HCISUPCMS.dll')) { return $candidate }
        }
        $dir = $dir.Parent
    }

    return $null
}

function Start-Api {
    if ((Get-ApiProcess).Count -gt 0) { Write-Host '[start] already running, use -Restart' -ForegroundColor Yellow; return }

    if ($Build) {
        Write-Host ('[build] ' + $Config + ' x64 ...') -ForegroundColor Cyan
        dotnet build $Solution -c $Config -p:Platform=x64 --nologo | Select-String ': error|succeeded|failed'
    }

    if (-not (Test-Path $ExePath)) { throw ('executable not found: ' + $ExePath + '（先执行 .\run.ps1 -Build）') }

    $sdk = Resolve-SdkDir
    if ($sdk) {
        $env:ISUP_SDK_DIR = $sdk
    } else {
        Write-Host '[warn] 未找到 ISUP SDK 原生库目录（HCISUPCMS.dll）；服务仍会启动，但 /health 会是 degraded。' -ForegroundColor Yellow
        Write-Host '       用 -SdkDir 指定，或设置 ISUP_SDK_DIR 环境变量。' -ForegroundColor Yellow
    }

    New-Item -ItemType Directory -Force -Path $RunDir | Out-Null
    Write-Host ('[start] ' + $ExePath) -ForegroundColor Cyan
    if ($sdk) { Write-Host ('        ISUP_SDK_DIR = ' + $sdk) }

    $proc = Start-Process -FilePath $ExePath -WorkingDirectory $ProjectDir -RedirectStandardOutput $OutLog -RedirectStandardError $ErrLog -WindowStyle Hidden -PassThru
    Write-Host ('[start] PID=' + $proc.Id + ' waiting...') -ForegroundColor Cyan

    for ($i = 0; $i -lt $WaitSeconds; $i++) {
        Start-Sleep -Seconds 1
        if (Get-Health) { Write-Host '[start] ready' -ForegroundColor Green; break }
        if ($proc.HasExited) {
            Write-Host ('[start] exited code=' + $proc.ExitCode) -ForegroundColor Red
            Get-Content $OutLog -Tail 30 -Encoding UTF8 -ErrorAction SilentlyContinue
            return
        }
    }

    Show-Status
}

if ($Status)  { Show-Status; return }
if ($Stop)    { Stop-Api; return }
if ($Restart) { Stop-Api; Start-Api; return }
Start-Api
