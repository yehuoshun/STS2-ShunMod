# build.ps1 — Windows PowerShell 构建脚本
# .\build.ps1                    仅构建
# .\build.ps1 -SyncDeps          构建 + 同步游戏 DLL 到 GitHub（CI 用）
# .\build.ps1 -Release v0.1.0    构建 + 上传到 Release

param(
    [switch]$SyncDeps,
    [string]$Release
)

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

# ============================================================
# 查找游戏
# ============================================================
$gamePaths = @(
    "C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2",
    "D:\Steam\steamapps\common\Slay the Spire 2",
    "E:\Steam\steamapps\common\Slay the Spire 2",
    "$env:ProgramFiles(x86)\Steam\steamapps\common\Slay the Spire 2"
)

$gamePath = $null
foreach ($p in $gamePaths) {
    if (Test-Path $p) { $gamePath = $p; break }
}
if (-not $gamePath) {
    Write-Host "❌ 找不到 Slay the Spire 2" -ForegroundColor Red
    exit 1
}
Write-Host "🎮 $gamePath\data_sts2_windows_x86_64" -ForegroundColor Cyan

# ============================================================
# 构建
# ============================================================
Write-Host "🔨 构建中..." -ForegroundColor Yellow
dotnet build -c Release
Write-Host "✅ 构建完成" -ForegroundColor Green

# ============================================================
# 同步 deps
# ============================================================
if ($SyncDeps) {
    $dataDir = "$gamePath\data_sts2_windows_x86_64"
    $tmpDir = Join-Path $env:TEMP "sts2mod-deps-$([Guid]::NewGuid())"
    New-Item -ItemType Directory -Path $tmpDir -Force | Out-Null

    try {
        Copy-Item "$dataDir\sts2.dll" $tmpDir
        Copy-Item "$dataDir\0Harmony.dll" $tmpDir

        $repo = (git remote get-url origin) -replace '.*github.com[:/](.*)\.git', '$1'

        # 删旧 deps
        gh release delete deps --yes --repo $repo 2>$null
        git push origin :refs/tags/deps 2>$null

        gh release create deps --repo $repo --title "CI Build Dependencies" `
            --notes "$(Get-Date -Format 'yyyy-MM-dd HH:mm') 自动同步" `
            "$tmpDir\sts2.dll" "$tmpDir\0Harmony.dll"

        Write-Host "✅ CI 依赖已同步" -ForegroundColor Green
    }
    finally {
        Remove-Item -Recurse -Force $tmpDir -ErrorAction SilentlyContinue
    }
    exit 0
}

# ============================================================
# 上传到 Release
# ============================================================
if ($Release) {
    $buildDir = "STS2-ShunMod\bin\Release\net9.0"
    $repo = (git remote get-url origin) -replace '.*github.com[:/](.*)\.git', '$1'
    $tmpDir = Join-Path $env:TEMP "sts2mod-rel-$([Guid]::NewGuid())"
    New-Item -ItemType Directory -Path $tmpDir -Force | Out-Null

    try {
        Copy-Item "$buildDir\STS2-ShunMod.dll" $tmpDir
        Copy-Item "STS2_ShunMod.json" $tmpDir
        if (Test-Path "$buildDir\STS2-ShunMod.pck") {
            Copy-Item "$buildDir\STS2-ShunMod.pck" $tmpDir
        }

        $zip = Join-Path $tmpDir "STS2-ShunMod.zip"
        Compress-Archive -Path "$tmpDir\*" -DestinationPath $zip -Force

        gh release upload $Release $zip --repo $repo --clobber
        Write-Host "✅ 已上传: https://github.com/$repo/releases/tag/$Release" -ForegroundColor Green
    }
    finally {
        Remove-Item -Recurse -Force $tmpDir -ErrorAction SilentlyContinue
    }
    exit 0
}
