<#
.SYNOPSIS
    运行 AssetBundle 对照测试

.DESCRIPTION
    此脚本在 Anity 模式和 Unity 模式下分别运行对照测试，验证行为一致性。

.PARAMETER UnityPath
    Unity DLL 路径。如果不指定，会自动检测。

.PARAMETER SkipAnity
    跳过 Anity 模式测试

.PARAMETER SkipUnity
    跳过 Unity 模式测试

.PARAMETER GenerateReport
    生成对照报告

.EXAMPLE
    .\run-compare-tests.ps1
    .\run-compare-tests.ps1 -GenerateReport
#>

param(
    [string]$UnityPath,
    [switch]$SkipAnity,
    [switch]$SkipUnity,
    [switch]$GenerateReport
)

$ErrorActionPreference = "Stop"

# ============================================================================
# Unity 检测
# ============================================================================

function Find-UnityInstallation {
    $hubPaths = @(
        "${env:ProgramFiles}\Unity\Hub\Editor",
        "${env:LocalAppData}\Unity\Hub\Editor"
    )

    foreach ($hubPath in $hubPaths) {
        if (Test-Path $hubPath) {
            $versions = Get-ChildItem $hubPath -Directory -ErrorAction SilentlyContinue | 
                Where-Object { $_.Name -match '^2022\.\d+\.\d+' } |
                Sort-Object Name -Descending

            foreach ($ver in $versions) {
                $managedPath = Join-Path $ver.FullName "Editor\Data\Managed"
                if (Test-Path $managedPath) {
                    return [PSCustomObject]@{
                        Version = $ver.Name
                        Path = $managedPath
                    }
                }
            }
        }
    }

    return $null
}

# ============================================================================
# 测试运行
# ============================================================================

function Run-TestsWithMode {
    param(
        [string]$Mode,
        [string]$UnityDllPath
    )

    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host "  运行 $Mode 模式测试" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan

    # 切换模式
    if ($Mode -eq "unity") {
        & .\scripts\switch-mode.ps1 -Mode unity -UnityPath $UnityDllPath -Force
    } else {
        & .\scripts\switch-mode.ps1 -Mode anity -Force
    }

    # 运行测试
    Write-Host "`n运行测试..." -ForegroundColor Yellow
    
    $testResult = dotnet test anity-lib-core\tests\Anity.AB.Compare.Tests\ `
        --no-restore `
        --logger "console;verbosity=detailed" `
        --results-directory ".\TestResults\$Mode"

    if ($LASTEXITCODE -eq 0) {
        Write-Host "$Mode 模式测试通过" -ForegroundColor Green
        return $true
    } else {
        Write-Host "$Mode 模式测试失败" -ForegroundColor Red
        return $false
    }
}

# ============================================================================
# 报告生成
# ============================================================================

function Generate-CompareReport {
    param(
        [bool]$AnityPassed,
        [bool]$UnityPassed,
        [string]$UnityVersion
    )

    $reportDir = ".\AB-Compare-Report"
    if (-not (Test-Path $reportDir)) {
        New-Item -ItemType Directory -Path $reportDir | Out-Null
    }

    $reportFile = Join-Path $reportDir "compare-report-$(Get-Date -Format 'yyyyMMdd-HHmmss').md"

    $report = @"
# AssetBundle 对照测试报告

生成时间: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")

## 测试环境

- Anity 版本: $(git describe --tags --always 2>$null || echo "dev")
- Unity 版本: $UnityVersion
- 测试平台: $(dotnet --version)
- 操作系统: $([System.Environment]::OSVersion.VersionString)

## 测试结果

| 测试模式 | 结果 |
|----------|------|
| Anity 模式 | $(if ($AnityPassed) { "✅ 通过" } else { "❌ 失败" }) |
| Unity 模式 | $(if ($UnityPassed) { "✅ 通过" } else { "❌ 失败" }) |

## 结论

$(if ($AnityPassed -and $UnityPassed) {
    "Anity 实现与 Unity 官方行为一致，所有测试通过。"
} elseif ($AnityPassed -and -not $UnityPassed) {
    "Anity 模式测试通过，但 Unity 模式测试失败。请检查 Unity DLL 配置。"
} elseif (-not $AnityPassed -and $UnityPassed) {
    "Unity 模式测试通过，但 Anity 模式测试失败。需要修复 Anity 实现。"
} else {
    "两种模式测试均失败。请检查测试环境和资源文件。"
})

## 详细日志

请查看以下目录获取详细测试日志:
- Anity 模式: .\TestResults\anity\
- Unity 模式: .\TestResults\unity\
"@

    Set-Content $reportFile $report
    Write-Host "`n报告已生成: $reportFile" -ForegroundColor Green
    
    return $reportFile
}

# ============================================================================
# 主逻辑
# ============================================================================

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Anity AssetBundle 对照测试" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# 检测 Unity
if (-not $UnityPath) {
    Write-Host "`n检测 Unity 安装..." -ForegroundColor Yellow
    $unity = Find-UnityInstallation
    if ($unity) {
        $UnityPath = $unity.Path
        Write-Host "检测到 Unity $($unity.Version)" -ForegroundColor Green
    } else {
        Write-Host "未检测到 Unity，将跳过 Unity 模式测试" -ForegroundColor Yellow
        $SkipUnity = $true
    }
}

# 运行测试
$anityPassed = $false
$unityPassed = $false

if (-not $SkipAnity) {
    $anityPassed = Run-TestsWithMode -Mode "anity"
}

if (-not $SkipUnity -and $UnityPath) {
    $unityPassed = Run-TestsWithMode -Mode "unity" -UnityDllPath $UnityPath
}

# 生成报告
if ($GenerateReport) {
    $unityVersion = if ($UnityPath) {
        $match = $UnityPath -match '(\d+\.\d+\.\d+f?\d*)'
        if ($match) { $Matches[1] } else { "Unknown" }
    } else { "Not Installed" }
    
    $reportFile = Generate-CompareReport -AnityPassed $anityPassed -UnityPassed $unityPassed -UnityVersion $unityVersion
    
    # 打开报告
    if (Test-Path $reportFile) {
        Start-Process notepad $reportFile
    }
}

# 恢复到 Anity 模式
Write-Host "`n恢复到 Anity 模式..." -ForegroundColor Yellow
& .\scripts\switch-mode.ps1 -Mode anity -Force

# 总结
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  测试完成" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

if ($anityPassed -and $unityPassed) {
    Write-Host "所有测试通过" -ForegroundColor Green
    exit 0
} elseif ($anityPassed) {
    Write-Host "Anity 模式测试通过" -ForegroundColor Yellow
    exit 0
} else {
    Write-Host "测试失败" -ForegroundColor Red
    exit 1
}
