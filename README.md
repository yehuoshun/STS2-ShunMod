# STS2-ShunMod 🦞

> 超级黑奴龙虾的杀戮尖塔 2 原生模组
> Shun's Slay the Spire 2 Mod — Native

[![Version](https://img.shields.io/badge/version-v0.0.0-blue)](https://github.com/yehuoshun/STS2-ShunMod)
[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE)
[![Framework](https://img.shields.io/badge/framework-.NET%209.0-purple)](STS2-ShunMod.csproj)

---

## 内容

### 🔮 超级神化 (Super Apotheosis)

| 属性 | 值 |
|---|---|
| 稀有度 | 稀有 · 无色 |
| 费用 | 2（升级后 1） |
| 类型 | 技能 · 自身 |
| 关键词 | 消耗 |

**效果**：升级战斗中所有卡牌，同时升级牌组中所有可升级卡牌。

灵感来自原版「神化」，但范围从手牌扩展到牌组。一张卡，全场牌组永久强化。

---

## 补丁

以下功能通过 Harmony 运行时注入实现，无需额外操作，安装即生效。

### ♾️ 无限升级

- `IsUpgradable` 始终返回 `true`——所有卡牌无限次升级
- 0 费卡每次升级累积 1 点能量，打出时释放

不再受「已升级」限制，找到多少升级事件就升多少次。0 费卡对多段升级收益尤高。

### 🛠️ 硬化外壳修复

修正原版「硬化外壳」能力的减伤数值计算异常。

---

## 项目结构

```
STS2-ShunMod/
├── STS2-ShunModCode/               # C# 源码
│   ├── MainFile.cs                 # Mod 入口（Harmony + 自动注册）
│   ├── Cards/
│   │   └── SuperApotheosis.cs      # 超级神化卡牌
│   ├── Core/
│   │   └── Registration/           # 自动注册系统
│   │       ├── PoolAttribute.cs    # [Pool] 特性
│   │       ├── AssemblyScanner.cs  # 安全类型扫描
│   │       └── ContentRegistry.cs  # 扫描 + 注册
│   ├── Patches/
│   │   ├── InfiniteUpgrade.cs      # 无限升级 + 0 费能量累积
│   │   └── HardenedShellPatch.cs   # 硬化外壳修复
│   └── Utils/
│       ├── ShunCard.cs             # 卡牌基类（链式配置）
│       └── RelicHelper.cs          # 遗物操作
├── STS2_ShunMod/                   # Godot 资源
│   ├── cards/                      # 卡牌美术
│   └── localization/               # 本地化（中/英）
├── project.godot                   # Godot 4.5 项目
└── STS2-ShunMod.csproj             # .NET 9.0
```

---

## 开发

### 添加新卡牌

继承 `ShunCard`，加 `[Pool]` 特性即可自动注册，**无需修改 MainFile**：

```csharp
[Pool(typeof(ColorlessCardPool))]
public class MyCard : ShunCard
{
    public MyCard()
        : base(baseCost: 1, type: CardType.Attack, rarity: CardRarity.Common, target: TargetType.Enemy)
    {
        WithKeywords(CardKeyword.Exhaust);
        WithTip(CardKeyword.Exhaust);
        WithCostUpgradeBy(-1);
    }

    public override string PortraitPath => "res://STS2_ShunMod/cards/my_card.png";

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 卡牌效果
    }
}
```

`ContentRegistry.RegisterAll()` 在启动时扫描所有 `[Pool]` 类并注册到卡池。

### 添加新补丁

```csharp
[HarmonyPatch(typeof(TargetClass), nameof(TargetClass.MethodName))]
public static class MyPatch
{
    static void Postfix(ref int __result) => __result = 42;
}
```

`MainFile.Initialize()` 中 `_harmony.PatchAll()` 自动应用。

---

## 构建

### 环境要求

- .NET 9.0 SDK
- Godot 4.5（.NET 版）
- 已安装 Slay the Spire 2

### 步骤

```bash
# 1. 编辑 Sts2PathDiscovery.props，设置 Sts2DataDir 指向 STS2 安装目录

# 2. 构建
dotnet build

# 构建产物自动复制到 Mods 目录：
#   STS2-ShunMod.dll / .json / .pck
```

---

## 安装

将 `STS2-ShunMod` 文件夹放入 Slay the Spire 2 的 `Mods/` 目录，启动游戏自动加载。

---

## 发布

### 本地发布

```powershell
.\build.ps1 -Publish v0.1.0   # 构建 + tag + push + Release
```

### CI 发布

```bash
git tag v0.1.0
git push --tags   # CI 自动构建 → 打包 → 发布 Release
```

首次使用前需同步游戏 DLL（游戏更新后也需重跑）：

```powershell
.\build.ps1 -SyncDeps
```

---

## 技术栈

- **游戏引擎** Godot 4.5 (.NET)
- **目标框架** .NET 9.0
- **Mod 框架** Harmony（运行时 IL 补丁）
- **打包** BSchneppe.StS2.PckPacker + Godot headless export

---

## 许可

MIT

---

## 作者

**yehuoshun** — 卷王龙虾，干就完了 🦞
