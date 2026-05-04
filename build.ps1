# build.ps1 — Windows PowerShell 构建脚本
# .\build.ps1                    仅构建
# .\build.ps1 -SyncDeps          构建 + 同步游戏 DLL 到 GitHub（CI 用）
# .\build.ps1 -Publish v0.1.0    构建 + 打包 + 打tag + 推送 + 创建Release + 上传
# .\build.ps1 -Release v0.1.0    构建 + 上传到已存在的 Release

param(
    [switch]$SyncDeps,
    [string]$Publish,
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
# 一键发布（构建 + tag + Release + 上传）
# ============================================================
if ($Publish) {
    $buildDir = "STS2-ShunMod\bin\Release\net9.0"
    $repo = (git remote get-url origin) -replace '.*github.com[:/](.*)\.git', '$1'
    $tmpDir = Join-Path $env:TEMP "sts2mod-pub-$([Guid]::NewGuid())"
    New-Item -ItemType Directory -Path $tmpDir -Force | Out-Null

    try {
        # 打包
        Copy-Item "$buildDir\STS2-ShunMod.dll" $tmpDir
        Copy-Item "STS2_ShunMod.json" $tmpDir
        if (Test-Path "$buildDir\STS2-ShunMod.pck") { Copy-Item "$buildDir\STS2-ShunMod.pck" $tmpDir }
        $zip = Join-Path $tmpDir "STS2-ShunMod.zip"
        Compress-Archive -Path "$tmpDir\*" -DestinationPath $zip -Force
        Write-Host "📦 打包完成" -ForegroundColor Cyan

        # 打 tag + 推送
        Write-Host "🏷️  创建 tag $Publish..." -ForegroundColor Yellow
        git tag $Publish
        git push origin $Publish

        # 生成更新日志
        $prev = (git describe --tags --abbrev=0 HEAD~1 2>$null) -replace '\s', ''
        $notes = @("## 更新内容", "")
        if ($prev) {
            $notes += "自 $prev 以来的更改："
            $notes += ""
        }
        $commits = if ($prev) { git log "$prev..HEAD" --pretty=format:"- %s ([%h](https://github.com/$repo/commit/%H))" --reverse }
                  else        { git log --pretty=format:"- %s ([%h](https://github.com/$repo/commit/%H))" --reverse }
        $notes += $commits
        $notesFile = Join-Path $tmpDir "release_notes.md"
        $notes -join "`n" | Out-File -FilePath $notesFile -Encoding utf8

        # 创建 Release + 上传
        Write-Host "🚀 创建 Release $Publish..." -ForegroundColor Yellow
        gh release create $Publish --repo $repo --title "Release $Publish" `
            --notes-file $notesFile $zip

        Write-Host "✅ 发布完成: https://github.com/$repo/releases/tag/$Publish" -ForegroundColor Green
    }
    finally {
        Remove-Item -Recurse -Force $tmpDir -ErrorAction SilentlyContinue
    }
    exit 0
}

# ============================================================
# 上传到已存在的 Release
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
