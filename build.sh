#!/bin/bash
# build.sh — 统一构建脚本
# 本地: ./build.sh                (自动找游戏路径，构建)
# 同步: ./build.sh --sync         (构建 + 上传 DLL 到 CI deps)
# CI:   ./build.sh --ci           (从 deps release 下载 DLL，构建)
set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
cd "$SCRIPT_DIR"
MODE="${1:-local}"

# ============================================================
# 查找游戏安装目录
# ============================================================
find_game() {
    local paths=(
        "C:/Program Files (x86)/Steam/steamapps/common/Slay the Spire 2"
        "D:/Steam/steamapps/common/Slay the Spire 2"
        "E:/Steam/steamapps/common/Slay the Spire 2"
        "$HOME/.local/share/Steam/steamapps/common/Slay the Spire 2"
        "$HOME/.steam/steam/steamapps/common/Slay the Spire 2"
        "$HOME/Library/Application Support/Steam/steamapps/common/Slay the Spire 2"
    )
    for p in "${paths[@]}"; do
        [ -d "$p" ] && { echo "$p"; return 0; }
    done
    return 1
}

data_subdir() {
    case "$(uname -s)" in
        Linux*)  echo "data_sts2_linuxbsd_x86_64" ;;
        Darwin*) echo "SlayTheSpire2.app/Contents/Resources/data_sts2_macos_x86_64" ;;
        *)       echo "data_sts2_windows_x86_64" ;;
    esac
}

# ============================================================
# 本地构建
# ============================================================
build_local() {
    local game=$(find_game)
    if [ -z "$game" ]; then
        echo "❌ 找不到 Slay the Spire 2"
        echo "   设置环境变量: export STS2_PATH=/path/to/Slay the Spire 2"
        exit 1
    fi
    local data="$game/$(data_subdir)"
    echo "🎮 $data"
    dotnet build -c Release -p:Sts2Path="$game"
}

# ============================================================
# CI 构建
# ============================================================
build_ci() {
    mkdir -p deps
    local repo=$(git remote get-url origin | sed 's/.*github.com[:/]\(.*\)\.git/\1/')
    echo "📥 下载 deps..."
    gh release download deps --dir deps --pattern "*.dll" --repo "$repo" --clobber || {
        echo "❌ 下载失败，请先在本地运行: ./build.sh --sync"
        exit 1
    }
    echo "🔨 构建..."
    dotnet build -c Release -p:Sts2DataDir="$(pwd)/deps"
}

# ============================================================
# 构建 + 同步 deps 到 CI
# ============================================================
build_and_sync() {
    build_local

    echo ""
    echo "📤 同步 DLL 到 CI deps..."

    if ! command -v gh &> /dev/null; then echo "❌ 需要 gh CLI: https://cli.github.com/"; exit 1; fi
    if ! gh auth status &> /dev/null; then echo "❌ 请先 gh auth login"; exit 1; fi

    local game=$(find_game)
    local data="$game/$(data_subdir)"
    local repo=$(git remote get-url origin | sed 's/.*github.com[:/]\(.*\)\.git/\1/')
    local tmp=$(mktemp -d)
    trap 'rm -rf "$tmp"' EXIT

    cp "$data/sts2.dll" "$tmp/"
    cp "$data/0Harmony.dll" "$tmp/"

    gh release delete deps --yes --repo "$repo" 2>/dev/null || true
    git push origin :refs/tags/deps 2>/dev/null || true
    gh release create deps --repo "$repo" --title "Build Dependencies" \
        --notes "$(date '+%Y-%m-%d %H:%M')" "$tmp/sts2.dll" "$tmp/0Harmony.dll"

    echo "✅ 同步完成"
}

# ============================================================
case "$MODE" in
    --ci)    build_ci ;;
    --sync)  build_and_sync ;;
    *)       build_local; echo ""; echo "💡 ./build.sh --sync  构建并同步到 CI" ;;
esac
