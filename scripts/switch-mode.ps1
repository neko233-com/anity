<#
.SYNOPSIS
    切换 Anity 项目在 Anity 模式和 Unity 模式之间

.DESCRIPTION
    此脚本用于在 Anity 自研实现模式和 Unity 官方 DLL 模式之间切换。
    - Anity 模式：使用 Anity.Core 中的自研实现，完全源码可控
    - Unity 模式：引用 Unity 官方 DLL，用于行为对照验证

.PARAMETER Mode
    目标模式：anity 或 unity

.PARAMETER UnityPath
    Unity DLL 路径（仅 Unity 模式需要）。如果不指定，会自动检测。

.PARAMETER Force
    强制切换，不提示确认

.EXAMPLE
    .\switch-mode.ps1 -Mode anity
    .\switch-mode.ps1 -Mode unity
    .\switch-mode.ps1 -Mode unity -UnityPath "C:\Program Files\Unity\Hub\Editor\2022.3.0f1\Editor\Data\Managed"
#>

param(
    [ValidateSet("anity", "unity")]
    [string]$Mode = "anity",
    
    [string]$UnityPath,
    
    [switch]$Force
)

$ErrorActionPreference = "Stop"

# ============================================================================
# Unity 安装检测
# ============================================================================

function Find-UnityInstallation {
    <#
    .SYNOPSIS
        自动检测 Unity 2022 安装路径
    #>
    
    $hubPaths = @(
        "${env:ProgramFiles}\Unity\Hub\Editor",
        "${env:LocalAppData}\Unity\Hub\Editor",
        "${env:ProgramFiles(x86)}\Unity\Hub\Editor"
    )

    $candidates = @()

    foreach ($hubPath in $hubPaths) {
        if (Test-Path $hubPath) {
            Write-Host "检测 Unity Hub: $hubPath" -ForegroundColor Gray
            
            $versions = Get-ChildItem $hubPath -Directory -ErrorAction SilentlyContinue | 
                Where-Object { $_.Name -match '^2022\.\d+\.\d+' } |
                Sort-Object Name -Descending

            foreach ($ver in $versions) {
                $managedPath = Join-Path $ver.FullName "Editor\Data\Managed"
                if (Test-Path $managedPath) {
                    $candidates += [PSCustomObject]@{
                        Version = $ver.Name
                        Path = $managedPath
                        IsLTS = $ver.Name -match '^\d+\.\d+\.f\d+$'
                        FullPath = $ver.FullName
                    }
                    Write-Host "  找到 Unity $($ver.Name)" -ForegroundColor Green
                }
            }
        }
    }

    # 检测传统安装路径
    $legacyPaths = @(
        "${env:ProgramFiles}\Unity\Editor",
        "C:\Program Files\Unity\Editor"
    )

    foreach ($legacyPath in $legacyPaths) {
        if (Test-Path $legacyPath) {
            $managedPath = Join-Path $legacyPath "Data\Managed"
            if (Test-Path $managedPath) {
                $candidates += [PSCustomObject]@{
                    Version = "Legacy"
                    Path = $managedPath
                    IsLTS = $false
                    FullPath = $legacyPath
                }
                Write-Host "  找到 Unity Legacy" -ForegroundColor Yellow
            }
        }
    }

    if ($candidates.Count -eq 0) {
        return $null
    }

    # 优先选择 Unity 2022 LTS
    $selected = $candidates | Where-Object { $_.Version -match '^2022\.' -and $_.IsLTS } | Select-Object -First 1
    if (-not $selected) {
        $selected = $candidates | Where-Object { $_.Version -match '^2022\.' } | Select-Object -First 1
    }
    if (-not $selected) {
        $selected = $candidates | Select-Object -First 1
    }

    return $selected
}

function Test-UnityDllIntegrity {
    <#
    .SYNOPSIS
        验证 Unity DLL 完整性
    #>
    param(
        [string]$Path
    )

    $requiredDlls = @(
        "UnityEngine.dll",
        "UnityEngine.CoreModule.dll",
        "UnityEditor.dll",
        "UnityEditor.CoreModule.dll"
    )

    $recommendedDlls = @(
        "UnityEngine.AssetBundleModule.dll",
        "UnityEngine.ImageConversionModule.dll",
        "UnityEngine.TextRenderingModule.dll",
        "UnityEngine.UI.dll"
    )

    $missing = @()
    $missingRecommended = @()

    foreach ($dll in $requiredDlls) {
        if (-not (Test-Path (Join-Path $Path $dll))) {
            $missing += $dll
        }
    }

    foreach ($dll in $recommendedDlls) {
        if (-not (Test-Path (Join-Path $Path $dll))) {
            $missingRecommended += $dll
        }
    }

    if ($missing.Count -gt 0) {
        throw "Unity DLL 不完整，缺少必需文件: $($missing -join ', ')"
    }

    if ($missingRecommended.Count -gt 0) {
        Write-Host "警告: 缺少推荐的 DLL: $($missingRecommended -join ', ')" -ForegroundColor Yellow
    }

    return $true
}

# ============================================================================
# 模式切换
# ============================================================================

function Switch-ToAnityMode {
    <#
    .SYNOPSIS
        切换到 Anity 自研模式
    #>
    
    Write-Host "`n切换到 Anity 自研模式..." -ForegroundColor Cyan
    
    # 更新 Directory.Build.props
    $propsFile = "anity-lib-core\Directory.Build.props"
    if (Test-Path $propsFile) {
        $content = Get-Content $propsFile -Raw
        if ($content -match 'UseUnityDll') {
            $content = $content -replace '<UseUnityDll>.*</UseUnityDll>', ''
            Set-Content $propsFile $content -NoNewline
        }
        Write-Host "  已更新 $propsFile" -ForegroundColor Green
    }

    # 更新 Anity.Core.csproj
    $csprojFile = "anity-lib-core\src\Anity.Core\Anity.Core.csproj"
    if (Test-Path $csprojFile) {
        $content = Get-Content $csprojFile -Raw
        if ($content -match 'UseUnityDll') {
            $content = $content -replace '<UseUnityDll>.*</UseUnityDll>', ''
            Set-Content $csprojFile $content -NoNewline
        }
        Write-Host "  已更新 $csprojFile" -ForegroundColor Green
    }

    Write-Host "`n已切换到 Anity 自研模式" -ForegroundColor Green
}

function Switch-ToUnityMode {
    <#
    .SYNOPSIS
        切换到 Unity 官方 DLL 模式
    #>
    param(
        [string]$UnityDllPath
    )
    
    Write-Host "`n切换到 Unity 官方 DLL 模式..." -ForegroundColor Cyan
    
    # 更新 Directory.Build.props
    $propsFile = "anity-lib-core\Directory.Build.props"
    if (Test-Path $propsFile) {
        $content = Get-Content $propsFile -Raw
        if ($content -notmatch 'UseUnityDll') {
            $content = $content -replace '</PropertyGroup>', "  <UseUnityDll>true</UseUnityDll>`n  </PropertyGroup>"
        }
        Set-Content $propsFile $content -NoNewline
        Write-Host "  已更新 $propsFile" -ForegroundColor Green
    }

    # 更新 Anity.Core.csproj
    $csprojFile = "anity-lib-core\src\Anity.Core\Anity.Core.csproj"
    if (Test-Path $csprojFile) {
        $content = Get-Content $csprojFile -Raw
        if ($content -notmatch 'UseUnityDll') {
            $content = $content -replace '</PropertyGroup>', "  <UseUnityDll>true</UseUnityDll>`n  </PropertyGroup>"
        }
        Set-Content $csprojFile $content -NoNewline
        Write-Host "  已更新 $csprojFile" -ForegroundColor Green
    }

    Write-Host "`n已切换到 Unity 官方 DLL 模式" -ForegroundColor Green
    Write-Host "Unity DLL 路径: $UnityDllPath" -ForegroundColor Gray
}

# ============================================================================
# 主逻辑
# ============================================================================

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Anity Unity 2022 模式切换工具" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Unity 模式需要 Unity DLL
if ($Mode -eq "unity") {
    # 自动检测 Unity 安装
    if (-not $UnityPath) {
        Write-Host "正在检测 Unity 安装..." -ForegroundColor Yellow
        $unity = Find-UnityInstallation
        
        if ($unity) {
            $UnityPath = $unity.Path
            Write-Host "检测到 Unity $($unity.Version)" -ForegroundColor Green
            Write-Host "路径: $UnityPath" -ForegroundColor Gray
        } else {
            Write-Host "未检测到 Unity 安装。" -ForegroundColor Red
            Write-Host "请使用 -UnityPath 参数指定 Unity DLL 路径" -ForegroundColor Yellow
            Write-Host "例如: .\switch-mode.ps1 -Mode unity -UnityPath 'C:\Program Files\Unity\Hub\Editor\2022.3.0f1\Editor\Data\Managed'" -ForegroundColor Yellow
            exit 1
        }
    }

    # 验证 DLL 完整性
    Write-Host "`n验证 Unity DLL..." -ForegroundColor Yellow
    try {
        Test-UnityDllIntegrity -Path $UnityPath
        Write-Host "DLL 验证通过" -ForegroundColor Green
    } catch {
        Write-Host $_.Exception.Message -ForegroundColor Red
        exit 1
    }
}

# 确认切换
if (-not $Force) {
    $currentMode = if (Test-Path "anity-lib-core\Directory.Build.props") {
        $content = Get-Content "anity-lib-core\Directory.Build.props" -Raw
        if ($content -match 'UseUnityDll.*true') { "unity" } else { "anity" }
    } else { "anity" }
    
    Write-Host "`n当前模式: $currentMode" -ForegroundColor Yellow
    Write-Host "目标模式: $Mode" -ForegroundColor Cyan
    
    $confirm = Read-Host "`n确认切换? (y/N)"
    if ($confirm -ne 'y' -and $confirm -ne 'Y') {
        Write-Host "已取消" -ForegroundColor Yellow
        exit 0
    }
}

# 执行切换
if ($Mode -eq "anity") {
    Switch-ToAnityMode
} else {
    Switch-ToUnityMode -UnityDllPath $UnityPath
}

# 恢复依赖
Write-Host "`n恢复依赖..." -ForegroundColor Yellow
dotnet restore
if ($LASTEXITCODE -eq 0) {
    Write-Host "依赖恢复成功" -ForegroundColor Green
} else {
    Write-Host "依赖恢复失败" -ForegroundColor Red
}

# 验证编译
Write-Host "`n验证编译..." -ForegroundColor Yellow
dotnet build anity-lib-core\src\Anity.Core\Anity.Core.csproj --no-restore -v quiet
if ($LASTEXITCODE -eq 0) {
    Write-Host "编译成功" -ForegroundColor Green
} else {
    Write-Host "编译失败" -ForegroundColor Red
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  完成！当前模式: $Mode" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
