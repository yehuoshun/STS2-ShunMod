using System.Text.RegularExpressions;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace STS2ShunMod.STS2_ShunModCode.Core;

/// <summary>
///     ShunMod 工具类 — 资源路径推导、遗物安全访问等通用方法。
/// </summary>
public static class ShunModHelper
{
    private const string ResourceRoot = "res://STS2_ShunMod/images";

    // ═══════════════════════════════════════════════════════════
    //  PascalCase → snake_case 编译正则
    // ═══════════════════════════════════════════════════════════
    //
    //  为什么用 static readonly + RegexOptions.Compiled：
    //  1. Regex.Replace(string, string, string) 静态重载每次调用都
    //     重新解析正则模式 + 编译内部匹配状态机，不会缓存。
    //  2. ClassNameToSnakeCase 被 CardPortraitPath / RelicIconPath /
    //     RelicOutlinePath / EventImagePath 调用，每张卡/遗物/事件
    //     初始化至少走一次，随着内容增多次数线性增长。
    //  3. static readonly 让模式只编译一次，CLR 类型初始化器保证
    //     线程安全。RegexOptions.Compiled 将正则编译为 IL，
    //     匹配速度比解释模式快 5-10x。
    //
    // ═══════════════════════════════════════════════════════════
    private static readonly Regex PascalToSnake = new("([a-z])([A-Z])", RegexOptions.Compiled);

    /// <summary>类名 → snake_case（如 ShunModSuperApotheosis → shun_mod_super_apotheosis）</summary>
    public static string ClassToSnakeCase(Type type)
    {
        return ClassNameToSnakeCase(type.Name);
    }

    /// <summary>类名字符串 → snake_case（使用编译正则，只初始化一次）</summary>
    public static string ClassNameToSnakeCase(string className)
    {
        return PascalToSnake.Replace(className, "$1_$2").ToLowerInvariant();
    }

    /// <summary>卡牌肖像路径：cards/shunCards/colorless/{snake}.png</summary>
    public static string CardPortraitPath(Type type, string color = "colorless")
    {
        return $"{ResourceRoot}/cards/shunCards/{color}/{ClassToSnakeCase(type)}.png";
    }

    /// <summary>遗物图标路径：relics/shunRelics/{snake}/{snake}.png</summary>
    public static string RelicIconPath(Type type)
    {
        var name = ClassToSnakeCase(type);
        return $"{ResourceRoot}/relics/shunRelics/{name}/{name}.png";
    }

    /// <summary>遗物描边图标路径：relics/shunRelics/{snake}/{snake}_outline.png</summary>
    public static string RelicOutlinePath(Type type)
    {
        var name = ClassToSnakeCase(type);
        return $"{ResourceRoot}/relics/shunRelics/{name}/{name}_outline.png";
    }

    /// <summary>事件图片路径：events/shunEvents/{snake}.png</summary>
    public static string EventImagePath(Type type)
    {
        return $"{ResourceRoot}/events/shunEvents/{ClassToSnakeCase(type)}.png";
    }

    // ═══════════════════════════════════════════════════════════
    //  遗物安全访问
    // ═══════════════════════════════════════════════════════════
    //
    //  为什么需要这个方法：
    //  某些 Mod 遗物没有注册到任何 Pool（如 EnergyIconHelper 所需的
    //  池），直接访问 RelicModel.HoverTips 会抛 InvalidOperationException。
    //  在事件 UI、遗物展示等场景中，这种异常应当被吞掉返回空提示，
    //  而不是让整个 UI 崩溃。
    //
    //  提取到 Core 层的原因：
    //  遗物悬浮提示的异常安全访问在多个场景（交易所、遗物展示、
    //  未来可能的事件）中都需要，避免每个模块重复写 try-catch。
    //
    // ═══════════════════════════════════════════════════════════
    /// <summary>安全获取遗物悬浮提示，捕获 Pool 缺失导致的异常。</summary>
    public static List<IHoverTip> SafeRelicHoverTips(RelicModel relic)
    {
        try
        {
            return relic.HoverTips.ToList();
        }
        catch (InvalidOperationException)
        {
            return new List<IHoverTip>();
        }
    }
}