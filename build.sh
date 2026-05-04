#!/bin/bash
# build.sh — 本地构建脚本
# ./build.sh              构建
# ./build.sh --release v0.1.0  构建 + 上传到 GitHub Release
set -e

cd "$(dirname "$0")"
MODE="${1:-build}"
TAG="${2:-}"

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
GAME=$(find_game)
if [ -z "$GAME" ]; then
    echo "❌ 找不到 Slay the Spire 2，设置 STS2_PATH 后重试"
    exit 1
fi
echo "🎮 $GAME/$(data_subdir)"

# 构建
dotnet build -c Release -p:Sts2Path="$GAME"
echo "✅ 构建完成"

# ============================================================
# 发布到 GitHub Release
# ============================================================
if [ "$MODE" = "--release" ]; then
    if [ -z "$TAG" ]; then
        echo "用法: ./build.sh --release v0.1.0"
        exit 1
    fi

    if ! command -v gh &> /dev/null; then echo "❌ 需要 gh CLI"; exit 1; fi
    if ! gh auth status &> /dev/null; then echo "❌ 请先 gh auth login"; exit 1; fi

    REPO=$(git remote get-url origin | sed 's/.*github.com[:/]\(.*\)\.git/\1/')
    BUILD_DIR="STS2-ShunMod/bin/Release/net9.0"
    TMP=$(mktemp -d)
    trap 'rm -rf "$TMP"' EXIT

    cp "$BUILD_DIR/STS2-ShunMod.dll" "$TMP/"
    cp STS2_ShunMod.json "$TMP/"
    cp "$BUILD_DIR/STS2-ShunMod.pck" "$TMP/" 2>/dev/null || echo "⚠️ 无 .pck"

    cd "$TMP"
    zip -r STS2-ShunMod.zip .

    echo "📤 上传到 $REPO Release $TAG..."
    gh release upload "$TAG" STS2-ShunMod.zip --repo "$REPO" --clobber

    echo "✅ 发布完成: https://github.com/$REPO/releases/tag/$TAG"
fi
