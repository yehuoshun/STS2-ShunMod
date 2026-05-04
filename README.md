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

### 🛠️ 硬化外壳修复
- 修正硬化外壳能力的减伤数值计算

---

## 项目结构

```
STS2-ShunMod/
├── STS2-ShunModCode/           # C# 源代码
│   ├── MainFile.cs             # Mod 入口，Harmony 补丁 + 自动注册
│   ├── Cards/
│   │   └── SuperApotheosis.cs  # 超级神化卡牌
│   ├── Core/
│   │   └── Registration/       # 自动注册系统
│   │       ├── PoolAttribute.cs   # [Pool] 属性
│   │       ├── AssemblyScanner.cs # 安全类型加载
│   │       └── ContentRegistry.cs # 属性扫描 + 自动注册
│   ├── Patches/
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

## 开发

### 添加新卡牌

只需新建类 + 加 `[Pool]` 属性，无需改 MainFile：

```csharp
[Pool(typeof(ColorlessCardPool))]
public class MyNewCard : ShunCard
{
    public MyNewCard()
        : base(baseCost: 1, type: CardType.Attack, rarity: CardRarity.Common, target: TargetType.Enemy)
    {
        // 链式配置关键词、升级等
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 卡牌效果
    }
}
```

`ContentRegistry.RegisterAll()` 在启动时自动扫描并注册所有带 `[Pool]` 属性的卡牌。

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

## 发布

### 发布流程

```bash
# 1. 本地构建
./build.sh

# 2. 创建 Release（CI 自动生成更新日志）
git tag v0.1.0
git push --tags

# 3. 上传构建产物
./build.sh --release v0.1.0
```

也可手动触发：Actions → Create Release → 填版本号

---

## 作者

**yehuoshun** — 卷王龙虾，干就完了 🦞
