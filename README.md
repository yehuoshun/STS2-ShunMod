# STS2-ShunMod

> 杀戮尖塔 2 原生模组
> Shun's Slay the Spire 2 Mod — Native

[![Version](https://img.shields.io/badge/version-v0.0.0-blue)](https://github.com/yehuoshun/STS2-ShunMod)
[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE)
[![Framework](https://img.shields.io/badge/framework-.NET%209.0-purple)](STS2-ShunMod.csproj)

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

---

## 补丁

| 名称 | 说明 |
|---|---|
| ♾️ 无限升级 | 卡牌可无限次升级。 |
| ✨ 无限附魔 | 卡牌可同时拥有多种附魔，同类叠加层数。通过复合包装器实现。 |
| 🛠️ 硬化外壳修复 | 修正硬化外壳能力的减伤数值。 |
| 🛡️ 格挡保留 | 格挡永不归零。 |
| 🔄 药水填充前移 | 使用/丢弃药水后，后方药水自动向前填充空位。 |
| 🌀 混沌药水保底 | 药水栏始终至少有一个混沌药水。开局/使用/丢弃后自动补充。 |
| 💥 显示总伤害 | 多段卡/X卡在卡牌描述末尾显示总伤害（单段伤害 × 段数）。 |

---

## 事件

| 名称 | 说明 |
|---|---|
| 🏪 遗物交易所 | ①随机遗物换随机遗物 ②随机遗物换卡牌附魔 ③扣5HP刷新 ④退出。可反复交易直到退出 |

---

## 安装

下载 Release 中的 `STS2-ShunMod.zip`，解压到 Slay the Spire 2 的 `Mods/STS2-ShunMod/` 目录，启动游戏自动加载。

---

## 项目结构

```
STS2-ShunMod/
├── STS2-ShunModCode/               # C# 源码
│   ├── MainFile.cs                 # Mod 入口（Harmony + 自动注册）
│   ├── Cards/
│   │   └── SuperApotheosis.cs      # 超级神化卡牌
│   ├── Relics/
│   │   └── ShunModBossTrophy.cs    # 首领奖杯遗物
│   ├── Events/
│   │   └── ShunModRelicExchange.cs   # 遗物交易所
│   ├── Core/
│   │   ├── ShunCard.cs             # 卡牌基类（链式配置）
│   │   ├── ShunLogger.cs           # 独立日志（logs/shunmod-YYYY-MM-DD.log）
│   │   ├── CreatureReflection.cs   # Creature 反射工具
│   │   ├── RelicHelper.cs          # 遗物反射工具
│   │   └── Registration/           # 自动注册系统
│   │       ├── PoolAttribute.cs    # [Pool] 特性
│   │       ├── AssemblyScanner.cs  # 安全类型扫描
│   │       └── ContentRegistry.cs  # 扫描 + 注册
│   └── Patches/
│       ├── Cards/
│       │   ├── InfiniteUpgrade.cs      # 无限升级
│       │   ├── InfiniteEnchant.cs      # 无限附魔
│       │   └── RepeatableCompositeEnchantment.cs  # 复合附魔包装器
│       ├── Combat/
│       │   ├── BlockRetentionPatch.cs  # 格挡保留
│       │   ├── HardenedShellPatch.cs   # 硬化外壳修复
│       │   └── ShowTotalDamage.cs      # 显示总伤害
│       ├── Events/
│       │   ├── ShunModEventRegistry.cs  # 事件注册
│       │   └── EventPortraitRedirectPatch.cs  # 事件肖像重定向
│       └── Potions/
│           └── PotionFillForwardPatch.cs  # 药水填充 + 混沌药水保底
├── STS2-ShunMod/                   # Godot 资源
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

    public override string PortraitPath => "res://STS2-ShunMod/cards/my_card.png";

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

## 调试

模组日志独立写入 `Mods/STS2-ShunMod/logs/shunmod-YYYY-MM-DD.log`，与游戏本体日志分离。

### 日志级别

在 `STS2-ShunMod.json` 中加 `"logLevel"` 字段控制输出量：

```json
{
  "id": "STS2-ShunMod",
  "logLevel": "Minimal"
}
```

| `logLevel` | 输出内容 | 适用场景 |
|---|---|---|
| `Minimal` | 只输出 ERROR | 正常玩游戏，零打扰 |
| `Normal` | ERROR + WARN + INFO（**默认**） | 日常开发，看补丁触发情况 |
| `Verbose` | 全部（含 DEBUG 状态快照 + TRACE 堆栈） | 追 bug |

不设此字段 = 默认 Normal。修改后**重启游戏**生效。

### 日志样例

```
[10:30:01.234] [INFO] [STS2-ShunMod] ══════════ 日志已启动 (级别: Normal) ══════════
[10:30:01.456] [INFO] [无限升级/TargetMethods] 扫描到 28 个 MaxUpgradeLevel getter
[10:35:22.790] [ERROR] [无限升级/反序列化] NullReferenceException: ...
[10:35:22.791] [TRACE] [无限升级/反序列化]    at STS2_ShunMod.Patches.InfiniteUpgrade_Deserialize.Finalizer(...)
```

---

## 发布

### 首次：上传游戏依赖

在 GitHub Releases 创建 tag 为 `deps` 的 release，上传游戏目录下的：

```
data_sts2_windows_x86_64/sts2.dll
data_sts2_windows_x86_64/0Harmony.dll
```

只做一次，之后 CI 自动拉取。

### 发布流程

推送代码到 main 分支后，手动触发构建：

**仓库 → Actions → Build & Release → Run workflow**

CI 自动完成：编译 → Godot 导出 .pck → 打包 ZIP → 成功后自动 bump 版本号并打 tag → 创建 GitHub Release 上传 ZIP。

> ⚠️ 只有 ZIP 打包成功才会打 tag，不会产出空 tag。

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

---

## 许可

MIT

---

## 作者

**yehuoshun** 和卷王龙虾，干就完了 🦞
