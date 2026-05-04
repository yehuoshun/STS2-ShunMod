#!/bin/bash
# update-deps.sh — 自动上传最新 sts2.dll / 0Harmony.dll 到 deps release
# 用法: ./scripts/update-deps.sh

set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_DIR="$(dirname "$SCRIPT_DIR")"
TEMP_DIR=$(mktemp -d)
trap 'rm -rf "$TEMP_DIR"' EXIT

echo "🔍 查找 Slay the Spire 2 安装路径..."

# 按优先级尝试各种路径
find_sts2() {
    local paths=(
        # Windows (Steam)
        "C:/Program Files (x86)/Steam/steamapps/common/Slay the Spire 2"
        "D:/Steam/steamapps/common/Slay the Spire 2"
        "E:/Steam/steamapps/common/Slay the Spire 2"
        # Windows (Steam user library)
        "$HOME/.steam/steam/steamapps/common/Slay the Spire 2"
        # Linux (Steam)
        "$HOME/.local/share/Steam/steamapps/common/Slay the Spire 2"
        "$HOME/.steam/steam/steamapps/common/Slay the Spire 2"
        # macOS (Steam)
        "$HOME/Library/Application Support/Steam/steamapps/common/Slay the Spire 2"
    )

    for p in "${paths[@]}"; do
        if [ -d "$p" ]; then
            echo "$p"
            return 0
        fi
    done
    return 1
}

# 检测平台和对应的数据目录
detect_data_dir() {
    local sts2_path="$1"
    case "$(uname -s)" in
        Linux*)     echo "$sts2_path/data_sts2_linuxbsd_x86_64" ;;
        Darwin*)    echo "$sts2_path/SlayTheSpire2.app/Contents/Resources/data_sts2_macos_x86_64" ;;
        CYGWIN*|MINGW*|MSYS*) echo "$sts2_path/data_sts2_windows_x86_64" ;;
    esac
}

STS2_PATH=$(find_sts2)
if [ -z "$STS2_PATH" ]; then
    echo "❌ 找不到 Slay the Spire 2 安装目录"
    echo "请设置环境变量 STS2_PATH 指向游戏目录后重试"
    echo "  export STS2_PATH=/path/to/Slay the Spire 2"
    exit 1
fi

DATA_DIR=$(detect_data_dir "$STS2_PATH")
echo "✅ 找到: $DATA_DIR"

# 检查文件
DLL_DIR="$DATA_DIR"
if [ ! -f "$DLL_DIR/sts2.dll" ]; then
    echo "❌ 找不到 sts2.dll"
    exit 1
fi
if [ ! -f "$DLL_DIR/0Harmony.dll" ]; then
    echo "❌ 找不到 0Harmony.dll"
    exit 1
fi

# 复制到临时目录
cp "$DLL_DIR/sts2.dll" "$TEMP_DIR/"
cp "$DLL_DIR/0Harmony.dll" "$TEMP_DIR/"

echo "📦 上传到 deps release..."
cd "$TEMP_DIR"

# 检查 gh CLI
if ! command -v gh &> /dev/null; then
    echo "❌ 需要 GitHub CLI: https://cli.github.com/"
    exit 1
fi

# 检查登录状态
if ! gh auth status &> /dev/null; then
    echo "❌ 请先登录: gh auth login"
    exit 1
fi

# 获取仓库名
REPO=$(cd "$PROJECT_DIR" && git remote get-url origin | sed 's/.*github.com[:/]\(.*\)\.git/\1/')

# 删除旧的 deps release（如果存在）
gh release delete deps --yes --repo "$REPO" 2>/dev/null || true
# 删除旧 tag
git push origin :refs/tags/deps 2>/dev/null || true

# 创建新的 deps release
gh release create deps \
    --repo "$REPO" \
    --title "Build Dependencies" \
    --notes "sts2.dll 和 0Harmony.dll 供 CI 构建使用。$(date '+%Y-%m-%d %H:%M') 更新。" \
    sts2.dll 0Harmony.dll

echo ""
echo "✅ deps release 更新完成！"
echo "现在可以推送 tag 触发自动构建: git tag vX.Y.Z && git push --tags"
