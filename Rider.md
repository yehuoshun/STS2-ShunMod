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

## 禁止的优化操作

- **不要删除 Harmony 方法的 `InconsistentNaming` 抑制** — `__result` 双下划线是 Harmony 约定，Rider 误报但必须保留
- **不要用 `// ReSharper disable All`** — 太粗暴，应为具体抑制类型
- **不要删除 `[SuppressMessage]` 属性** — 即使看起来没用，可能是 Harmony 反射调用的方法