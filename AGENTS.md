# AGENTS.md — STS2-ShunMod

> 面向 AI 编程助手的项目速查手册。人类请读 README.md。

---

## 项目概要

- **是什么**：杀戮尖塔 2 (Slay the Spire 2) 原生模组，4 模块
- **技术栈**：C# / .NET 9.0 / Godot 4.5 / Harmony IL 补丁
- **入口**：`STS2_ShunMod.sln`

---

## 模块依赖（单向无环）

```
ShunMod.Core          ← 基础框架，无依赖
  ↑
  ├── ShunMod.Shun    ← 引用 Core，舜角色内容（卡牌/遗物/事件）
  ├── ShunMod.Tweaks  ← 引用 Core，游戏机制修改
  └── ShunMod.Compat  ← 引用 Core，第三方模组兼容
```

**各模块初始化流程**：`ModEntry.Initialize()` → `new Harmony(ModId)` → `PatchAll()` → （Shun 额外走 ContentRegistry 注册内容）

---

## 改 X 看哪些文件

| 需求 | 文件 |
|------|------|
| 加新卡牌 | `ShunMod.Shun/Cards/` 新建类，继承 `CardModel`，加 `[CardPool]` 特性 |
| 加新遗物 | `ShunMod.Shun/Relics/` 新建类，继承 `RelicModel`，加 `[RelicPool]` 特性 |
| 加新事件 | `ShunMod.Shun/Events/` 新建类，继承 `EventModel`，加 `[EventPool]` 特性 |
| 加游戏机制修改 | `ShunMod.Tweaks/Patches/` 新建 Harmony Patch 类 |
| 加第三方兼容 | `ShunMod.Compat/Patches/Compatibility/` 新建类，在 `CompatibilityPatches.ApplyAll()` 中注册 |
| 改 Core 框架工具 | `ShunMod.Core/Core/` 下对应工具类 |
| 改本地化文本 | `ShunMod.Shun/assets/localization/` 下对应 JSON |
| 改卡牌/遗物图片 | `ShunMod.Shun/assets/images/` |

---

## 核心机制

### 内容自动注册（Core）

`ContentRegistry.RegisterAll(assembly)` 扫描 `[CardPool]` / `[RelicPool]` / `[EventPool]` 特性，自动注册到游戏卡池。

- 卡牌/遗物：直接 `ModHelper.AddModelToPool()`
- 事件：只收集类型到 `OnEventTypeFound` 回调，延迟实例化（见下方）

### 事件注册流程（Shun，最复杂的部分）

```
1. ContentRegistry 扫描 [EventPool] → 触发 OnEventTypeFound 回调
2. ShunModEventRegistry.AddEventType() 收集类型
3. ModelDbInitSafePatch (HarmonyPrefix) 劫持 ModelDb.Init
   → 跳过原版 Init（避免 DuplicateModelException）
   → SafeInit 遍历 AllAbstractModelSubtypes，手动创建实例
   → 为 mod 事件类型手动创建实例并注册到 ShunModEventRegistry
4. AllSharedEventsInjectPatch (HarmonyPostfix) 注入到 AllSharedEvents
   → 兜底：如果 SafeInit 没跑，这里自动创建
```

### 兼容补丁约定（Compat）

- 所有兼容补丁**纯反射实现**，不硬依赖目标模组 DLL
- 统一入口 `CompatibilityPatches.ApplyAll()`，按类型分组
- 每个补丁类有独立的 `Apply(Harmony)` 静态方法

### 共享补丁模式（LimitPatchHelper）

- `BgLimitPatch` / `SkinLimitPatch` 共用 `LimitPatchHelper` 的 Transpiler 模式
- `AppliedFlag` 包装 bool 解决闭包捕获限制（C# 不允许局部函数捕获 ref 参数）
- `OnAssemblyLoad` 延迟加载兜底：目标模组 DLL 未加载时订阅 AssemblyLoad 事件

---

## 编码约定

> Rider 代码检查规则（SuppressMessage、Harmony 命名约定等）详见 [`Rider.md`](./Rider.md)，修改代码前先读。

- **命名空间**按模块：`ShunMod.Core.xxx` / `ShunMod.Shun.xxx` / `ShunMod.Tweaks.xxx` / `ShunMod.Compat.xxx`
- **Harmony Patch** 类命名：`{功能}Patch`，放在 `Patches/` 目录下
- **日志前缀**：统一用 `ModId` 常量，不要 `HarmonyId` 别名或 `var id` 中间变量
- **资源路径**：用 `ShunCard.PortraitPath<T>()` / `ShunRelic.IconPath<T>()` / `ShunModHelper.EventImagePath(type)` 生成，不要硬编码路径字符串
- **ModEntry 单例**：每个模块的 `Initialize()` 有 `lock` 防重入
- **事件图片**：`EventPortraitRedirectPatch` 有 Texture2D 缓存，改事件图片路径逻辑要看这里
- **守卫优先，减少嵌套**：能提前 return 就别用 if 包。嵌套控制在 2 层以内。
  ```csharp
  // ❌ 不要
  if (FindType(ns, type) is { } t) { DoSomething(t); }
  // ✅ 要
  if (FindType(ns, type) is not { } t) return;
  DoSomething(t);
  ```
- **每次修改后必须 `git commit + push`**，commit message 用中文

---

## 构建

### 本地 build

- 全量：`dotnet build`（或 Rider 按 F9）
- 单模块：`dotnet build ShunMod.Shun/ShunMod.Shun.csproj`
- Rider 已配好 4 个独立 Build 配置（Build: Core/Shun/Tweaks/Compat），可添加到 Services 窗口管理

### CI

GitHub Actions 自动触发，无需本地 build。

---

## 已知坑点

1. **事件注册**是最容易翻车的地方。ModelDb.Init 原版会抛 `DuplicateModelException`，所以 SafeInit 跳过了它。改事件相关逻辑时，先理清 `ModelDbInitSafePatch → AllSharedEventsInjectPatch` 这条链。
2. **Compat 模块的补丁是反射调用**，目标类型不存在时静默跳过，不会报错。调试时如果补丁不生效，先检查目标模组是否真的装了。
3. **ContentRegistry 的 else if 链**是刻意设计——一个类只能标记一种 Pool 类型，双重标记会被静默跳过。
4. **事件图片缓存**在 `EventPortraitRedirectPatch` 是静态 Dictionary，不是线程安全的，但游戏主线程不会并发，所以安全。
5. **Shun 模块的 .pck 打包**由 BSchneppe.StS2.PckPacker 在构建后自动执行，改 assets 后要重新 build。
6. **AssemblyLoad 延迟加载**：兼容补丁的 `OnAssemblyLoad` 订阅了 `AppDomain.CurrentDomain.AssemblyLoad`，改 `Apply` 逻辑时注意取消订阅，防止内存泄漏。
7. **Rider 独立模块 Build**：配置在 `.idea/runConfigurations/`，只有 build 不 run。添加到 Services 窗口后可单独 build 任意模块。