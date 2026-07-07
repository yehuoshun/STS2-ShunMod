# STS2-ShunMod

> 杀戮尖塔 2 原生模组
> Shun's Slay the Spire 2 Mod — Native

[![Version](https://img.shields.io/github/v/release/yehuoshun/STS2-ShunMod)](https://github.com/yehuoshun/STS2-ShunMod/releases)
[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE)
[![Framework](https://img.shields.io/badge/framework-.NET%209.0-purple)](STS2_ShunMod.csproj)

---

## 卡牌

| 名称 | 费用 | 稀有度 | 类型 | 效果 |
|---|---|---|---|---|
| 超级神化 | 2→1 | 稀有·无色 | 技能 | 升级战斗中所有卡牌，同时升级牌组中所有可升级卡牌。 |

---

## 遗物

| 名称 | 稀有度 | 效果 |
|---|---|---|
| 🏆 首领奖杯 | 稀有 | 击杀 Boss 后最大生命值 +25%。 |
| 🌿 丰饶叶 | 稀有 | 每个回合开始时，用随机药水填满所有空药水栏位。 |
| 💪 无限壶铃 | 稀有 | 在休息处可无限次锻炼，每次锻炼后战斗开始时多获得 1 点力量。 |

---

## 补丁

| 名称 | 说明 |
|---|---|
| ♾️ 无限升级 | 卡牌可无限次升级。 |
| 🛠️ 硬化外壳修复 | 修正硬化外壳能力的减伤数值。 |
| 🛡️ 格挡保留 | 格挡永不归零。 |
| 💥 显示总伤害 | 多段卡/X卡在卡牌描述末尾显示总伤害（单段伤害 × 段数）。 |
| ⚔️ 锻造拉回 | 所有锻造行为自动将非手牌的君王之剑拉回手牌。 |
| ⚡ 能量保留 | 回合开始时能量不清零，剩余能量累积（冰激凌逻辑）。 |

## 兼容性补丁

| 名称 | 说明 |
|---|---|
| 🔮 影之诗进化不消耗+回合解除 | 进化不消耗进化点（TryUseEvolutionPoint 跳过原方法），初始 1 点启动。回合限制一并解除，每回合可多次进化。纯反射，无依赖。 |
| 🎨 影之诗皮肤限制解除 | 影之诗模组皮肤启用数从 14→无限，Patch SkinPackManager.SetEnabled。纯反射，无依赖。 |
| 🖼️ 影之诗背景包限制解除 | 影之诗模组背景包启用数从 7→无限，Patch BgPackManager。纯反射，无依赖。 |

---

## 事件

| 名称 | 说明 |
|---|---|
| 🏪 遗物交易所 | ①随机遗物换随机遗物 ②随机遗物换卡牌附魔 ③扣5HP刷新 ④退出。可反复交易直到退出 |

---

## 安装

下载 Release 中的 `STS2_ShunMod.zip`，解压到 Slay the Spire 2 的 `Mods/STS2_ShunMod/` 目录，启动游戏自动加载。

---

## 项目结构

```
STS2-ShunMod/
├── STS2_ShunModCode/               # C# 源码
│   ├── ModEntry.cs                 # Mod 入口（Harmony + 自动注册）
│   ├── Cards/
│   │   └── Shun/
│   │       └── ShunModSuperApotheosis.cs  # 超级神化卡牌
│   ├── Relics/
│   │   └── Shun/
│   │       ├── ShunModBossTrophy.cs      # 首领奖杯遗物
│   │       ├── ShunModBountifulFrond.cs  # 丰饶叶遗物
│   │       └── ShunModInfiniteGirya.cs   # 无限壶铃遗物
│   ├── Events/
│   │   └── Shun/
│   │       └── ShunModRelicExchange.cs   # 遗物交易所
│   ├── Core/
│   │   ├── ContentRegistry.cs          # 扫描 + 注册
│   │   ├── PoolAttribute.cs            # [CardPool]/[RelicPool]/[EventPool] 特性
│   │   ├── ShunModHelper.cs            # 资源路径工具
│   │   ├── ShunRelic.cs                # 遗物路径工具（静态泛型 helper）
│   │   ├── ShunCard.cs                 # 卡牌肖像工具（静态泛型 helper）
│   │   ├── CompatibilityPatchUtil.cs   # 兼容补丁共享工具（类型查找/单例发现）
│   │   └── CreatureReflection.cs        # Creature 反射工具（Block/IsPlayer）
│   └── Patches/
│       ├── Compatibility/
│       │   ├── CompatibilityPatches.cs              # 统一入口
│       │   ├── ShadowverseEvolutionPointPatch.cs   # 影之诗进化点解除
│       │   ├── ShadowverseSkinLimitPatch.cs        # 影之诗皮肤限制解除
│       │   └── ShadowverseBgLimitPatch.cs          # 影之诗背景包限制解除
│       ├── Cards/
│       │   └── InfiniteUpgrade.cs        # 无限升级
│       ├── Combat/
│       │   ├── BlockRetentionPatch.cs    # 格挡保留
│       │   ├── EnergyRetentionPatch.cs   # 能量保留（冰激凌）
│       │   ├── HardenedShellPatch.cs     # 硬化外壳修复
│       │   ├── ShowTotalDamage.cs        # 显示总伤害
│       │   └── ForgePullBladesToHandPatch.cs  # 锻造拉回君王之剑
│       ├── Events/
│       │   └── ShunModEventRegistry.cs   # 事件注册 + 注入补丁
├── STS2_ShunMod/                   # Godot 资源
│   ├── cards/                      # 卡牌美术
│   └── localization/               # 本地化（中/英）
├── STS2_ShunMod.json               # 模组清单
├── project.godot                   # Godot 4.5 项目
└── STS2_ShunMod.csproj             # .NET 9.0
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
- 已安装 Slay the Spire 2

### 本地构建

```bash
# 编辑 Sts2PathDiscovery.props，设置 Sts2Path 指向 STS2 安装目录
dotnet build
```

产物输出到 `.godot/mono/temp/bin/Release/`。

---

## 技术栈

- **游戏引擎** Godot 4.5 (.NET)
- **目标框架** .NET 9.0
- **Mod 框架** Harmony（运行时 IL 补丁）
- **打包** BSchneppe.StS2.PckPacker + Godot headless export

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
