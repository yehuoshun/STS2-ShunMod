# STS2-ShunMod 🦞

> Shun's Slay the Spire 2 Mod — 超级黑奴龙虾的杀戮尖塔2原生模组

## 功能

### 🔮 超级神化 (Super Apotheosis)
- **稀有度**：稀有 · 无色
- **费用**：2（升级后 1）
- **类型**：技能 · 自身
- **关键词**：消耗
- **效果**：升级战斗中所有卡牌，同时升级牌组中所有可升级卡牌

### ♾️ 无限升级
- `IsUpgradable` 始终返回 `true`，卡牌可无限次升级
- 0 费卡每次升级累积 1 点能量，打出时释放

### 🏪 遗物交易所
- 自定义事件，可在游戏中遇到
- 选项 1：交出 1 个遗物 → 随机能力卡
- 选项 2：交出 2 个遗物 → 带注能附魔的能力卡
- 选项 3：免费获取本 Mod 随机卡牌

### 📋 空白能力卡
- 打出时执行本地存档中储存的所有附加能力

### 🛠️ 硬化外壳修复
- 修正硬化外壳能力的减伤数值计算

---

## 项目结构

```
STS2-ShunMod/
├── STS2-ShunModCode/           # C# 源代码
│   ├── MainFile.cs             # Mod 入口，Harmony 补丁 + 卡池注册
│   ├── Cards/
│   │   └── SuperApotheosis.cs  # 超级神化卡牌
│   ├── Patches/
│   │   ├── EventRegistry.cs    # 自定义事件注入
│   │   ├── HardenedShellPatch.cs
│   │   └── InfiniteUpgrade.cs  # 无限升级 + 能量累积
│   └── Utils/
│       ├── ShunCard.cs         # 卡牌基类（链式配置）
│       └── RelicHelper.cs      # 遗物操作工具
├── STS2_ShunMod/               # Godot 资源
│   ├── cards/                  # 卡牌图片
│   └── localization/           # 本地化（中/英）
├── project.godot               # Godot 4.5 项目配置
└── STS2-ShunMod.csproj         # .NET 9.0 项目文件
```

---

## 构建

### 环境要求
- .NET 9.0 SDK
- Godot 4.5（.NET 版）
- 已安装 Slay the Spire 2

### 构建步骤

```bash
# 1. 配置 Sts2PathDiscovery.props（指向 STS2 安装目录）
#    编辑 Sts2PathDiscovery.props，设置 Sts2DataDir 路径

# 2. 构建
dotnet build

# 3. 构建产物自动复制到 Mods 文件夹：
#    - STS2-ShunMod.dll
#    - STS2-ShunMod.json
#    - STS2-ShunMod.pck
```

构建后 DLL + manifest 自动复制到 `$(ModsPath)/STS2-ShunMod/`。

---

## 安装

1. 将 `STS2-ShunMod` 文件夹放入 Slay the Spire 2 的 `Mods/` 目录
2. 启动游戏，Mod 自动加载

---

## 技术栈

- **游戏引擎**：Godot 4.5 (.NET)
- **目标框架**：.NET 9.0
- **Mod 框架**：Harmony（运行时 IL 补丁）
- **打包**：BSchneppe.StS2.PckPacker + Godot --headless --export-pack

---

## 许可

MIT

---

## 作者

**yehuoshun** — 卷王龙虾，干就完了 🦞
