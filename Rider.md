# Rider 使用笔记

_面向 AI 编程助手的 Rider 代码检查速查手册。_

---

## Harmony 命名约定抑制

Harmony 的 `__instance` / `__result` / `__exception` 参数命名与 Rider 命名规则冲突，必须显式抑制：

```csharp
[SuppressMessage("ReSharper", "InconsistentNaming")]
private static void Postfix(NRelic instance, ref int __result) { }
```

或文件级注释（多个 Harmony 方法时用）：

```csharp
// ReSharper disable InconsistentNaming
```

## Rider 代码检查方式

| 操作 | 路径 |
|------|------|
| 全量检查 | 右键项目 → **代码检查** → **检查代码** |
| 快速扫描 | **分析** → **检查代码** → 选 C# 代码风格 + 冗余模式 |
| 实时检查 | 编辑器右上角色条 → 展开 **问题** 工具窗口 |
| 一键清理 | **分析** → **代码清理** → 选项目 → 配置规则 |

## 常见 Rider 警告及对应 SuppressMessage

| Rider 警告 | 场景 | 抑制方式 |
|------------|------|----------|
| `InconsistentNaming` | Harmony `__instance`/`__result`/`__exception` 参数 | `[SuppressMessage("ReSharper", "InconsistentNaming")]` |
| `RedundantAssignment` | Harmony `ref __result` 直接覆盖不读原值 | `[SuppressMessage("ReSharper", "RedundantAssignment")]` |
| `UnusedMember.Local` | Harmony 反射调用的 Postfix/Prefix 方法 | `[SuppressMessage("ReSharper", "UnusedMember.Local")]` |
| `UnusedType.Global` | Harmony 反射发现的 Patch 类 / ModEntry 类 | `[SuppressMessage("ReSharper", "UnusedType.Global")]` |
| `RedundantAssignment` | 对 Harmony `ref` 参数赋值（如 `evolvePoints = 1`） | `[SuppressMessage("ReSharper", "RedundantAssignment")]` |

## Rider 代码检查体系

### 核心引擎

- 底层跑的是 **ReSharper 的静态分析引擎**
- C# 部分同时集成 **Roslyn 分析器**（微软官方 + NuGet analyzer）

### 扫描策略

| 策略 | 说明 |
|------|------|
| **全方案分析**（Solution-Wide Analysis） | 跨文件依赖追踪，修改一个文件影响全方案警告 |
| **增量分析** | 只重扫变更文件及其依赖，不改的不算 |
| **延迟/滑动窗口** | 大方案打开时，后台按优先级逐步扫描，不卡编辑器 |

### 检查分类

1. **代码质量问题** — 潜在 bug、空引用、未使用的变量等
2. **代码风格/命名规范**
3. **冗余检测** — 未使用的 using、多余的 cast、可简化的表达式
4. **通用模式** — 设计模式、最佳实践
5. **拼写检查**
6. **各语言专项** — XAML、ASP.NET、JavaScript、JSON 等

### 严重级别

```
Error → Warning → Suggestion → Hint
```

### 配置方式（优先级从高到低）

1. `.editorconfig` — 项目级，可入 Git
2. `.sln.DotSettings` — 方案级，可入 Git 团队共享
3. Settings Layer 覆盖 — 当前层级覆盖下级

### 性能策略

- 大方案默认开 **退出延迟模式**：停止输入 2 秒后才开始扫
- 可手动切 `Full Solution Analysis` 关闭范围

---

## 我写代码必须遵守的 Rider 规则

### Harmony 命名约定抑制

Harmony 的 `__instance` / `__result` / `__exception` 参数命名与 Rider 命名规则冲突，必须显式抑制：

```csharp
[SuppressMessage("ReSharper", "InconsistentNaming")]
private static void Postfix(NRelic instance, ref int __result) { }
```

或文件级注释（多个 Harmony 方法时用）：

```csharp
// ReSharper disable InconsistentNaming
```

### Rider 代码检查方式

| 操作 | 路径 |
|------|------|
| 全量检查 | 右键项目 → **代码检查** → **检查代码** |
| 快速扫描 | **分析** → **检查代码** → 选 C# 代码风格 + 冗余模式 |
| 实时检查 | 编辑器右上角色条 → 展开 **问题** 工具窗口 |
| 一键清理 | **分析** → **代码清理** → 选项目 → 配置规则 |

### 常见 Rider 警告及对应 SuppressMessage

| Rider 警告 | 场景 | 抑制方式 |
|------------|------|----------|
| `InconsistentNaming` | Harmony `__instance`/`__result`/`__exception` 参数 | `[SuppressMessage("ReSharper", "InconsistentNaming")]` |
| `RedundantAssignment` | Harmony `ref __result` 直接覆盖不读原值 | `[SuppressMessage("ReSharper", "RedundantAssignment")]` |
| `UnusedMember.Local` | Harmony 反射调用的 Postfix/Prefix 方法 | `[SuppressMessage("ReSharper", "UnusedMember.Local")]` |
| `UnusedType.Global` | Harmony 反射发现的 Patch 类 / ModEntry 类 | `[SuppressMessage("ReSharper", "UnusedType.Global")]` |
| `RedundantAssignment` | 对 Harmony `ref` 参数赋值（如 `evolvePoints = 1`） | `[SuppressMessage("ReSharper", "RedundantAssignment")]` |

### 禁止的优化操作

- **不要删除 Harmony 方法的 `InconsistentNaming` 抑制** — `__result` 双下划线是 Harmony 约定，Rider 误报但必须保留
- **不要用 `// ReSharper disable All`** — 太粗暴，应为具体抑制类型
- **不要删除 `[SuppressMessage]` 属性** — 即使看起来没用，可能是 Harmony 反射调用的方法
- **不要批量修改命名空间或全局重命名** — 除非明确要求，否则只改目标文件
- **不要自动清理整个文件的 using** — 可能移除 Harmony 反射需要的隐式引用