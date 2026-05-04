#!/bin/bash
# build.sh — 本地构建与发布
# ./build.sh                    仅构建
# ./build.sh --sync-deps        构建 + 同步游戏 DLL 到 GitHub（CI 用）
# ./build.sh --release v0.1.0   构建 + 打包 + 上传到已存在的 Release
set -e

cd "$(dirname "$0")"
MODE="${1:-build}"
ARG="${2:-}"

find_game() {
    local paths=(
        "C:/Program Files (x86)/Steam/steamapps/common/Slay the Spire 2"
        "D:/Steam/steamapps/common/Slay the Spire 2"
        "E:/Steam/steamapps/common/Slay the Spire 2"
        "$HOME/.local/share/Steam/steamapps/common/Slay the Spire 2"
        "$HOME/.steam/steam/steamapps/common/Slay the Spire 2"
        "$HOME/Library/Application Support/Steam/steamapps/common/Slay the Spire 2"
    )
    for p in "${paths[@]}"; do [ -d "$p" ] && { echo "$p"; return 0; }; done
    return 1
}

data_subdir() {
    case "$(uname -s)" in
        Linux*)  echo "data_sts2_linuxbsd_x86_64" ;;
        Darwin*) echo "SlayTheSpire2.app/Contents/Resources/data_sts2_macos_x86_64" ;;
        *)       echo "data_sts2_windows_x86_64" ;;
    esac
}

GAME=$(find_game)
[ -z "$GAME" ] && { echo "❌ 找不到 Slay the Spire 2"; exit 1; }
echo "🎮 $GAME/$(data_subdir)"

# 构建
dotnet build -c Release -p:Sts2Path="$GAME"
echo "✅ 构建完成"

# ============================================================
# 同步游戏 DLL 给 CI
# ============================================================
if [ "$MODE" = "--sync-deps" ]; then
    command -v gh &> /dev/null || { echo "❌ 需要 gh CLI"; exit 1; }
    gh auth status &> /dev/null || { echo "❌ 请先 gh auth login"; exit 1; }

    REPO=$(git remote get-url origin | sed 's/.*github.com[:/]\(.*\)\.git/\1/')
    DATA="$GAME/$(data_subdir)"
    TMP=$(mktemp -d)
    trap 'rm -rf "$TMP"' EXIT

    cp "$DATA/sts2.dll" "$TMP/"
    cp "$DATA/0Harmony.dll" "$TMP/"

    gh release delete deps --yes --repo "$REPO" 2>/dev/null || true
    git push origin :refs/tags/deps 2>/dev/null || true
    gh release create deps --repo "$REPO" --title "CI Build Dependencies" \
        --notes "$(date '+%Y-%m-%d %H:%M') 自动同步" \
        "$TMP/sts2.dll" "$TMP/0Harmony.dll"

    echo "✅ CI 依赖已更新。之后 git tag vX.Y.Z && git push --tags 即可自动发布。"
    exit 0
fi

# ============================================================
# 上传到 Release
# ============================================================
if [ "$MODE" = "--release" ]; then
    TAG="${ARG:-$(git describe --tags --abbrev=0 2>/dev/null)}"
    [ -z "$TAG" ] && { echo "用法: ./build.sh --release v0.1.0"; exit 1; }

    command -v gh &> /dev/null || { echo "❌ 需要 gh CLI"; exit 1; }
    gh auth status &> /dev/null || { echo "❌ 请先 gh auth login"; exit 1; }

    REPO=$(git remote get-url origin | sed 's/.*github.com[:/]\(.*\)\.git/\1/')
    BUILD_DIR="STS2-ShunMod/bin/Release/net9.0"
    TMP=$(mktemp -d)
    trap 'rm -rf "$TMP"' EXIT

    cp "$BUILD_DIR/STS2-ShunMod.dll" "$TMP/"
    cp STS2_ShunMod.json "$TMP/"
    cp "$BUILD_DIR/STS2-ShunMod.pck" "$TMP/" 2>/dev/null || echo "⚠️ 无 .pck"
    cd "$TMP" && zip -r STS2-ShunMod.zip . && cd "$OLDPWD"

    gh release upload "$TAG" "$TMP/STS2-ShunMod.zip" --repo "$REPO" --clobber
    echo "✅ 已上传: https://github.com/$REPO/releases/tag/$TAG"
    exit 0
fi
