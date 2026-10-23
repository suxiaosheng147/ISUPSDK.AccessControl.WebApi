# ============================================================================
#  ISUP 门禁接入 Web API —— IIS 应用池配置 / 核对
#
#  背景：本项目以 hostingModel="inprocess" 部署，应用进程就是 IIS 工作进程（w3wp）。
#        IIS 应用池默认「空闲 20 分钟 → 终止工作进程」，一旦发生：
#          - ISUP SDK 的 7660（设备注册）/ 8081（ESS 图片存储）监听断开，设备掉线；
#          - 定时同步 / 校时、Digest 轮询、事件管道、异步下发任务 全部停止；
#          - 只能等下一个 HTTP 请求冷启动才恢复；SDK 初始化失败时不会自愈，只降级（/health=degraded）。
#
#  用法（必须以管理员身份运行 PowerShell）：
#    .\iis-apppool.ps1                          只读核对（默认），打印 PASS/FAIL
#    .\iis-apppool.ps1 -Apply                   执行修改
#    .\iis-apppool.ps1 -Apply -Restart          修改后立刻回收应用池并做健康检查
#    .\iis-apppool.ps1 -RecycleTime 03:30:00    每日定时回收时间（默认 00:00:00）
#    .\iis-apppool.ps1 -AllowOverlapping        允许重叠回收（默认禁用，原因见下）
#    .\iis-apppool.ps1 -PoolName X -SiteName Y  指定别的应用池 / 站点
#
#  为什么要禁用重叠回收（disallowOverlappingRotation = True）：
#    IIS 默认允许重叠回收：先起新 w3wp、再关旧 w3wp。旧进程退出前仍占着 7660/8081，
#    新进程的 NET_ECMS_StartListen 会失败；而 IsupSdkHostedService 启动失败只记日志、不退出，
#    结果是「进程活着、设备接入是废的、而且不会自愈」。禁用重叠后先停旧再起新，宁可短暂断连。
#
#  为什么回收时间默认 00:00：默认的固定间隔（25 小时）会在白天任意时刻硬重启；
#    改成每天固定时间回收，把「必然发生的中断」放到业务低峰。
#
#  注意：web.config 里的 applicationInitialization 只负责「进程起来后立刻预热」，
#        「进程不被空闲杀掉」必须靠这里配的应用池设置（applicationHost.config 级别，web.config 写不了）。
# ============================================================================
[CmdletBinding()]
param(
    [string]$PoolName = 'ISUPSDK.AccessControl.WebApi.git',
    [string]$SiteName = 'ISUPSDK.AccessControl.WebApi.git',
    [string]$RecycleTime = '00:00:00',
    [string]$HealthUrl = 'http://127.0.0.1:7823/health',
    [switch]$Apply,
    [switch]$Restart,
    [switch]$AllowOverlapping
)

$ErrorActionPreference = 'Stop'

try { Import-Module WebAdministration -ErrorAction Stop }
catch { throw '无法加载 WebAdministration：请用「管理员身份」运行 PowerShell，并确认已安装 IIS 管理工具。' }

$PoolPath = 'IIS:\AppPools\' + $PoolName
$SitePath = 'IIS:\Sites\' + $SiteName
if (-not (Test-Path $PoolPath)) { throw ('应用池不存在：' + $PoolName) }
if (-not (Test-Path $SitePath)) { throw ('站点不存在：' + $SiteName) }

try { $WantRecycle = [TimeSpan]::Parse($RecycleTime) }
catch { throw ('RecycleTime 格式不对（要 HH:mm:ss，例如 00:00:00）：' + $RecycleTime) }

$Results = New-Object System.Collections.ArrayList

function Add-Result([string]$Item, [string]$State, [string]$Detail) {
    [void]$Results.Add([pscustomobject]@{ Item = $Item; State = $State; Detail = $Detail })
}

function ConvertTo-Text($Value) {
    if ($Value -is [bool]) {
        if ($Value) { return 'True' }
        return 'False'
    }
    if ($Value -is [TimeSpan]) { return $Value.ToString() }
    return [string]$Value
}

# 读 → 比对 → 只在 -Apply 时才修
function Sync-Setting([string]$Item, $Actual, $Expected, [scriptblock]$Fix) {
    if ($Actual -eq $Expected) {
        Add-Result $Item 'PASS' (ConvertTo-Text $Actual)
        return
    }
    $detail = (ConvertTo-Text $Actual) + ' -> ' + (ConvertTo-Text $Expected)
    if (-not $Apply) {
        Add-Result $Item 'FAIL' $detail
        return
    }
    & $Fix
    Add-Result $Item 'FIXED' $detail
}

Write-Host ''
Write-Host ('[pool] ' + $PoolName + '    [site] ' + $SiteName) -ForegroundColor Cyan
if ($Apply) {
    Write-Host '[mode] APPLY：修改应用池 / 站点设置' -ForegroundColor Yellow
} else {
    Write-Host '[mode] 只读核对（要改加 -Apply）' -ForegroundColor Yellow
}

$pool = Get-Item $PoolPath
$site = Get-Item $SitePath

# 1) 根因：空闲超时
Sync-Setting 'processModel.idleTimeout（空闲超时）' $pool.processModel.idleTimeout ([TimeSpan]::Zero) {
    Set-ItemProperty $PoolPath -Name processModel.idleTimeout -Value ([TimeSpan]::Zero)
}

# 2) 常驻：应用池总是运行
Sync-Setting 'startMode（启动模式）' $pool.startMode 'AlwaysRunning' {
    Set-ItemProperty $PoolPath -Name startMode -Value 'AlwaysRunning'
}

# 3) 预热：站点 preloadEnabled（需 Application Initialization 功能，Windows Server 默认已装）
Sync-Setting '站点 preloadEnabled（预热）' $site.applicationDefaults.preloadEnabled $true {
    Set-ItemProperty $SitePath -Name applicationDefaults.preloadEnabled -Value $true
}

# 4) 关掉「固定间隔」回收
Sync-Setting 'recycling.periodicRestart.time（固定间隔回收）' $pool.recycling.periodicRestart.time ([TimeSpan]::Zero) {
    Set-ItemProperty $PoolPath -Name recycling.periodicRestart.time -Value ([TimeSpan]::Zero)
}

# 5) 每日定时回收（先清空再写，可重复执行）
$schedule = @($pool.recycling.periodicRestart.schedule.Collection | ForEach-Object { [TimeSpan]$_.value })
$scheduleText = '(无)'
if ($schedule.Count -gt 0) { $scheduleText = ($schedule | ForEach-Object { $_.ToString() }) -join ',' }
$hasWanted = $false
foreach ($t in $schedule) { if ($t -eq $WantRecycle) { $hasWanted = $true } }

if ($hasWanted) {
    Add-Result 'recycling.periodicRestart.schedule（每日回收）' 'PASS' $scheduleText
} elseif ($Apply) {
    Clear-ItemProperty $PoolPath -Name recycling.periodicRestart.schedule -ErrorAction SilentlyContinue
    Set-ItemProperty $PoolPath -Name recycling.periodicRestart.schedule -Value @{ value = $WantRecycle.ToString() }
    Add-Result 'recycling.periodicRestart.schedule（每日回收）' 'FIXED' ($scheduleText + ' -> ' + $WantRecycle.ToString())
} else {
    Add-Result 'recycling.periodicRestart.schedule（每日回收）' 'FAIL' ($scheduleText + ' -> ' + $WantRecycle.ToString())
}

# 6) 重叠回收（默认禁用，见文件头说明）
$wantOverlapDisabled = -not $AllowOverlapping
Sync-Setting 'recycling.disallowOverlappingRotation（禁用重叠回收）' $pool.recycling.disallowOverlappingRotation $wantOverlapDisabled {
    Set-ItemProperty $PoolPath -Name recycling.disallowOverlappingRotation -Value $wantOverlapDisabled
}

# 7) 以下只核对、不自动改（改动影响大，交给人决定）
if ($pool.processModel.maxProcesses -eq 1) {
    Add-Result 'processModel.maxProcesses（Web 园）' 'PASS' '1'
} else {
    Add-Result 'processModel.maxProcesses（Web 园）' 'FAIL' ((ConvertTo-Text $pool.processModel.maxProcesses) + ' -> 必须是 1（7660/8081 只能被一个进程持有）')
}

if ($pool.managedPipelineMode -eq 'Integrated') {
    Add-Result 'managedPipelineMode（托管管道）' 'PASS' 'Integrated'
} else {
    Add-Result 'managedPipelineMode（托管管道）' 'FAIL' ((ConvertTo-Text $pool.managedPipelineMode) + ' -> 期望 Integrated（ASP.NET Core 只支持集成管道；IIS 管理器改完回收一次）')
}

if ($pool.enable32BitAppOnWin64) {
    Add-Result 'enable32BitAppOnWin64（32 位）' 'FAIL' '已启用 -> 必须关闭（ISUP 原生库是 x64）'
} else {
    Add-Result 'enable32BitAppOnWin64（32 位）' 'PASS' 'False'
}

# ---------------------------------------------------------------------------
# 结果表（逐项一行带颜色；中文字符在 Format-Table 下对齐不可靠）
# ---------------------------------------------------------------------------
Write-Host ''
foreach ($r in $Results) {
    $color = 'Green'
    if ($r.State -eq 'FAIL') { $color = 'Red' }
    elseif ($r.State -eq 'FIXED') { $color = 'Yellow' }
    Write-Host ('   [' + $r.State.PadRight(5) + '] ' + $r.Item + ' : ' + $r.Detail) -ForegroundColor $color
}

# ---------------------------------------------------------------------------
# 运行时现状
# ---------------------------------------------------------------------------
Write-Host '[runtime]' -ForegroundColor Cyan

$worker = @(Get-CimInstance Win32_Process -Filter "Name='w3wp.exe'" -ErrorAction SilentlyContinue |
    Where-Object { $_.CommandLine -like ('*-ap "' + $PoolName + '"*') })
if ($worker.Count -eq 0) {
    Write-Host '   w3wp: 未运行（应用池未启动，或刚被回收）' -ForegroundColor Yellow
} else {
    foreach ($w in $worker) {
        Write-Host ('   w3wp: PID=' + $w.ProcessId + '  启动于 ' + $w.CreationDate)
    }
}

foreach ($port in 7660, 8081) {
    $conn = @(Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue)
    if ($conn.Count -gt 0) {
        $owner = Get-Process -Id $conn[0].OwningProcess -ErrorAction SilentlyContinue
        Write-Host ('   port ' + $port + ': ' + $owner.ProcessName + ' (PID=' + $conn[0].OwningProcess + ')')
    } else {
        Write-Host ('   port ' + $port + ': 未监听（SDK 没起来，或进程已停）') -ForegroundColor Yellow
    }
}

try {
    $health = Invoke-RestMethod $HealthUrl -TimeoutSec 5
    $mark = 'OK (' + $health.status + ')'
    if ($health.status -ne 'healthy') { $mark = 'DEGRADED (' + $health.status + ')' }
    Write-Host ('   health: ' + $mark + '  onlineDevices=' + $health.onlineDevices)
} catch {
    Write-Host ('   health: 无响应 ' + $HealthUrl) -ForegroundColor Yellow
}

$webConfig = Join-Path $site.PhysicalPath 'web.config'
if (Test-Path $webConfig) {
    $text = [System.IO.File]::ReadAllText($webConfig)
    if ($text -match 'applicationInitialization') {
        Write-Host '   web.config: 含 applicationInitialization（预热已配）'
    } else {
        Write-Host '   web.config: 没有 applicationInitialization（缺预热：把仓库里的 web.config 复制到发布目录）' -ForegroundColor Yellow
    }
    if ($text -match 'hostingModel\s*=\s*"inprocess"') {
        Write-Host '   web.config: hostingModel=inprocess'
    } else {
        Write-Host '   web.config: hostingModel 不是 inprocess' -ForegroundColor Yellow
    }
} else {
    Write-Host ('   web.config: 不存在 ' + $webConfig) -ForegroundColor Yellow
}

# ---------------------------------------------------------------------------
# 汇总
# ---------------------------------------------------------------------------
$fail = @($Results | Where-Object { $_.State -eq 'FAIL' }).Count
$fixed = @($Results | Where-Object { $_.State -eq 'FIXED' }).Count
Write-Host ''
if ($fail -eq 0) {
    if ($fixed -gt 0) {
        Write-Host ('[result] 全部通过（本次修复 ' + $fixed + ' 项）') -ForegroundColor Green
    } else {
        Write-Host '[result] 全部通过' -ForegroundColor Green
    }
} else {
    Write-Host ('[result] 有 ' + $fail + ' 项不达标，见上表') -ForegroundColor Red
}

if ($Restart) {
    if ($Apply) {
        Write-Host ('[restart] 回收应用池 ' + $PoolName + ' ...') -ForegroundColor Cyan
        Restart-WebAppPool -Name $PoolName
        Start-Sleep -Seconds 5
        try {
            $after = Invoke-RestMethod $HealthUrl -TimeoutSec 20
            Write-Host ('[restart] /health: ' + $after.status + '  onlineDevices=' + $after.onlineDevices) -ForegroundColor Green
        } catch {
            Write-Host '   /health 还没起来：SDK 初始化较慢，稍后再看 /health' -ForegroundColor Yellow
        }
    } else {
        Write-Host '[restart] 已忽略：-Restart 需要和 -Apply 一起用' -ForegroundColor Yellow
    }
}

if (-not $Apply -and $fail -gt 0) { exit 1 }
