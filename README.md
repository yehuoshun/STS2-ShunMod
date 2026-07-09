# STS2-ShunMod

> 杀戮尖塔 2 原生模组
> Shun's Slay the Spire 2 Mod — Native

[![Version](https://img.shields.io/github/v/release/yehuoshun/STS2-ShunMod)](https://github.com/yehuoshun/STS2-ShunMod/releases)
[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE)
[![Framework](https://img.shields.io/badge/framework-.NET%209.0-purple)](STS2_ShunMod.sln)

---

## 模块说明

| 模块 | 文件夹 | 说明 |
|---|---|---|
| `ShunMod_Core` | `Mods/ShunMod_Core/` | 共享基础框架（必需） |
| `ShunMod_Shun` | `Mods/ShunMod_Shun/` | 舜角色内容（卡牌/遗物/事件） |
| `ShunMod_Tweaks` | `Mods/ShunMod_Tweaks/` | 数值/机制修改 |
| `ShunMod_Compat` | `Mods/ShunMod_Compat/` | 第三方模组兼容补丁 |

---

## 卡牌

| 名称 | 费用 | 稀有度 | 类型 | 效果 |
|---|---|---|---|---|
| 超级神化 | 2→1 | 稀有·无色 | 技能 | 升级战斗中所有卡牌，同时升级牌组中所有可升级卡牌。
| 永远打击 | 0 | 普通·无色 | 攻击 | 永远 | 造成 6(9) 点伤害。打出后永远回到手牌。

---

## 遗物

| 名称 | 稀有度 | 效果 |
|---|---|---|
| 🏆 首领奖杯 | 稀有 | 击杀 Boss 后最大生命值 +25%。 |
| 🌿 丰饶叶 | 稀有 | 每个回合开始时，用随机药水填满所有空药水栏位。 |
| 💪 无限壶铃 | 稀有 | 在休息处可无限次锻炼，每次锻炼后战斗开始时多获得 1 点力量。 |

---

## 补丁

| 名称 | 模块 | 说明 |
|---|---|---|
| ♾️ 无限升级 | Tweaks | 卡牌可无限次升级。 |
| 🛠️ 硬化外壳修复 | Tweaks | 修正硬化外壳能力的减伤数值。 |
| 🛡️ 格挡保留 | Tweaks | 格挡永不归零。 |
| 💥 显示总伤害 | Tweaks | 多段卡/X卡在卡牌描述末尾显示总伤害（单段伤害 × 段数）。 |
| ⚔️ 锻造拉回 | Tweaks | 所有锻造行为自动将非手牌的君王之剑拉回手牌。 |
| ⚡ 能量保留 | Tweaks | 回合开始时能量不清零，剩余能量累积（冰激凌逻辑）。 |

## 兼容性补丁

| 名称 | 模块 | 说明 |
|---|---|---|
| 🔮 影之诗进化不消耗+回合解除 | Compat | 进化不消耗进化点（TryUseEvolutionPoint 跳过原方法），初始 1 点启动。回合限制一并解除，每回合可多次进化。纯反射，无依赖。 |
| 🎨 影之诗皮肤限制解除 | Compat | 影之诗模组皮肤启用数从 14→无限，Patch SkinPackManager.SetEnabled。纯反射，无依赖。 |
| 🖼️ 影之诗背景包限制解除 | Compat | 影之诗模组背景包启用数从 7→无限，Patch BgPackManager。纯反射，无依赖。 |

---

## 事件

| 名称 | 模块 | 说明 |
|---|---|---|
| 🏪 遗物交易所 | Shun | ①随机遗物换随机遗物 ②随机遗物换卡牌附魔 ③扣5HP刷新 ④退出。可反复交易直到退出 |

---

## 安装

下载 Release 中的 `STS2_ShunMod.zip`，解压到 Slay the Spire 2 的 `Mods/` 目录，得到 4 个模块文件夹：

```
Mods/
├── ShunMod_Core/          # 必需
├── ShunMod_Shun/          # 舜角色内容
├── ShunMod_Tweaks/        # 数值修改（可选，部分功能依赖 Core）
└── ShunMod_Compat/        # 兼容补丁（可选，无 Shadowverse 模组不生效）
```

也可以单独下载各模块的独立 zip 按需部署。

---

## 项目结构

```
STS2-ShunMod/
├── STS2_ShunMod.sln                    # 4 项目 Solution
│
├── ShunMod.Core/                       # 共享基础框架
│   ├── ShunMod.Core.csproj             # AssemblyName: ShunMod_Core
│   ├── ShunMod_Core.json               # 独立模组清单
│   ├── ModEntry.cs                     # Harmony.PatchAll（保留入口）
│   └── Core/
│       ├── ContentRegistry.cs          # 扫描 + 注册（回调解耦）
│       ├── PoolAttribute.cs            # [CardPool]/[RelicPool]/[EventPool] 特性
│       ├── ShunModHelper.cs            # 资源路径工具
│       ├── ShunRelic.cs                # 遗物路径工具（静态泛型 helper）
│       ├── ShunCard.cs                 # 卡牌肖像工具（静态泛型 helper）
│       ├── ShunEvent.cs                # 事件基类
│       ├── CompatibilityPatchUtil.cs   # 兼容补丁共享工具（类型查找/单例发现）
│       ├── CreatureReflection.cs       # Creature 反射工具（Block/IsPlayer）
│       └── DynamicVarHelper.cs         # DynamicVar 反射赋值工具
│
├── ShunMod.Shun/                       # 舜角色内容
│   ├── ShunMod.Shun.csproj             # AssemblyName: ShunMod_Shun → 引用 Core
│   ├── ShunMod_Shun.json               # 模组清单，依赖 ShunMod_Core
│   ├── ModEntry.cs                     # Harmony.PatchAll + ContentRegistry
│   ├── Cards/
│   │   ├── ShunModStokeModified.cs     # 添柴·改
│   │   └── ShunModSuperApotheosis.cs   # 超级神化
│   ├── Relics/
│   │   ├── ShunModBossTrophy.cs        # 首领奖杯
│   │   ├── ShunModBountifulFrond.cs    # 丰饶叶
│   │   └── ShunModInfiniteGirya.cs     # 无限壶铃
│   └── Events/
│       ├── ShunModRelicExchange.cs     # 遗物交易所
│       └── ShunModEventRegistry.cs     # 事件注册 + 注入补丁
│
├── ShunMod.Tweaks/                     # 数值/机制修改
│   ├── ShunMod.Tweaks.csproj           # AssemblyName: ShunMod_Tweaks → 引用 Core
│   ├── ShunMod_Tweaks.json             # 模组清单，依赖 ShunMod_Core
│   ├── ModEntry.cs                     # Harmony.PatchAll
│   └── Patches/
│       ├── Combat/
│       │   ├── BlockRetentionPatch.cs      # 格挡保留
│       │   ├── EnergyRetentionPatch.cs     # 能量保留（冰激凌）
│       │   ├── ForgePullBladesToHandPatch.cs  # 锻造拉回君王之剑
│       │   ├── HardenedShellPatch.cs       # 硬化外壳修复
│       │   └── ShowTotalDamage.cs          # 显示总伤害
│       └── Cards/
│           └── InfiniteUpgrade.cs          # 无限升级
│
├── ShunMod.Compat/                     # 兼容性补丁
│   ├── ShunMod.Compat.csproj           # AssemblyName: ShunMod_Compat → 引用 Core
│   ├── ShunMod_Compat.json             # 模组清单，依赖 ShunMod_Core
│   ├── ModEntry.cs                     # Harmony.PatchAll + CompatibilityPatches
│   └── Patches/Compatibility/
│       ├── CompatibilityPatches.cs         # 统一入口
│       ├── ShadowverseEvolutionPointPatch.cs  # 影之诗进化点解除
│       ├── ShadowverseSkinLimitPatch.cs       # 影之诗皮肤限制解除
│       └── ShadowverseBgLimitPatch.cs         # 影之诗背景包限制解除
│
│   └── assets/                         # Godot 资源（图片/本地化）
│       ├── images/                     # 卡牌/遗物/事件美术
│       └── localization/               # 中英双语本地化
├── project.godot                       # Godot 4.5 项目
├── Sts2PathDiscovery.props             # 路径发现配置
└── .github/workflows/                  # CI/CD
```

---

## 开发

### 添加新卡牌

继承 `CardModel`，加 `[CardPool]` 特性即可自动注册：

```csharp
[CardPool(typeof(ColorlessCardPool))]
public class MyCard : CardModel
{
    public MyCard()
        : base(1, CardType.Attack, CardRarity.Common, TargetType.Enemy)
    {
    }

    public override string PortraitPath => ShunCard.PortraitPath<MyCard>();

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 卡牌效果
    }
}
```

`ShunMod.Shun/ModEntry.cs` 中的 `ContentRegistry.RegisterAll()` 在启动时扫描所有 `[CardPool]` / `[RelicPool]` / `[EventPool]` 类并注册到对应池。

> 卡牌、遗物、事件分别对应 `[CardPool]`、`[RelicPool]`、`[EventPool]` 特性，路径工具类 `ShunCard` / `ShunRelic` / `ShunEvent` 自动生成资源路径。

### 添加新补丁

写入对应模块的 `Patches/` 目录即可：

```csharp
namespace ShunMod.Tweaks.Combat;  // 或 ShunMod.Compat

[HarmonyPatch(typeof(TargetClass), nameof(TargetClass.MethodName))]
public static class MyPatch
{
    static void Postfix(ref int __result) => __result = 42;
}
```

各模块的 `ModEntry.Initialize()` 中 `_harmony.PatchAll()` 自动应用。

### 添加新兼容补丁（ShunMod.Compat）

在 `CompatibilityPatches.ApplyAll()` 中追加调用：

```csharp
internal static class CompatibilityPatches
{
    public static void ApplyAll(Harmony harmony)
    {
        // ── 在此追加新的兼容补丁 ──
        MyNewPatch.Apply(harmony);
    }
}
```

---

## 构建

### 环境要求

- .NET 9.0 SDK
- 已安装 Slay the Spire 2

### 本地构建

```bash
# 编辑 Sts2PathDiscovery.props，设置 Sts2Path 指向 STS2 安装目录
dotnet build STS2_ShunMod.sln
```

产物输出到 `.godot/mono/temp/bin/Release/`，每个模块独立 dll：
- `ShunMod_Core.dll`
- `ShunMod_Shun.dll` + `ShunMod_Shun.pck`（PckPacker 自动打包）
- `ShunMod_Tweaks.dll`
- `ShunMod_Compat.dll`

### 按需构建单个模块

```bash
dotnet build ShunMod.Shun/ShunMod.Shun.csproj -c Release
```

---

## 技术栈

- **游戏引擎** Godot 4.5 (.NET)
- **目标框架** .NET 9.0
- **Mod 框架** Harmony（运行时 IL 补丁）
- **打包** BSchneppe.StS2.PckPacker（仅 Shun 模块）
- **CI/CD** GitHub Actions

---

## 鸣谢

- **[STS2Plus](https://github.com/StephenSHorton/STS2Plus)**
- **[YuWanCard / 鱼丸](https://github.com/YuWan886/Sts2-YuWanCard)**
- **[onakasuitanie](https://space.bilibili.com/3493298765301961?spm_id_from=333.788.upinfo.detail.click)**

---

## 许可

MIT

---

## 作者

**yehuoshun** 和卷王龙虾，干就完了 🦞