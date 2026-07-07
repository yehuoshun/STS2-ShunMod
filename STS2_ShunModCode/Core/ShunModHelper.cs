using System.Text.RegularExpressions;

namespace STS2ShunMod.STS2_ShunModCode.Core;

/// <summary>
///     ShunMod 资源路径工具 — 从类名自动推导图片路径。
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
}