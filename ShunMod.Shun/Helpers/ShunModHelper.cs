using System.Text.RegularExpressions;

namespace ShunMod.Shun.Helpers;

/// <summary>
///     ShunMod 工具类 — 资源路径推导。
/// </summary>
public static class ShunModHelper
{
    private const string ResourceRoot = "res://ShunMod_Shun/images";
    private const string ShunModCardsPath = "cards/shunCards";

    private const string ShunModRelicsPath = "relics/shunRelics";

    // PascalCase → snake_case 编译正则
    // 不使用 [GeneratedRegex]：MonoMod 的 JIT Hook 与源生成器冲突，运行时崩溃
#pragma warning disable SYSLIB1045
    private static readonly Regex PascalToSnake = new("([a-z])([A-Z])", RegexOptions.Compiled);
#pragma warning restore SYSLIB1045

    /// <summary>类名 → snake_case（如 ShunModSuperApotheosis → shun_mod_super_apotheosis）</summary>
    private static string ClassToSnakeCase(Type type)
    {
        return ClassNameToSnakeCase(type.Name);
    }

    /// <summary>类名字符串 → snake_case（使用编译正则，只初始化一次）</summary>
    private static string ClassNameToSnakeCase(string className)
    {
        return PascalToSnake.Replace(className, "$1_$2").ToLowerInvariant();
    }

    /// <summary>卡牌肖像路径：cards/shunCards/colorless/{snake}.png</summary>
    public static string CardPortraitPath(Type type, string color = "colorless")
    {
        return $"{ResourceRoot}/{ShunModCardsPath}/{color}/{ClassToSnakeCase(type)}.png";
    }

    /// <summary>遗物图标路径：relics/shunRelics/{snake}/{snake}.png</summary>
    public static string RelicIconPath(Type type)
    {
        var name = ClassToSnakeCase(type);
        return $"{ResourceRoot}/{ShunModRelicsPath}/{name}/{name}.png";
    }

    /// <summary>遗物描边图标路径：relics/shunRelics/{snake}/{snake}_outline.png</summary>
    public static string RelicOutlinePath(Type type)
    {
        var name = ClassToSnakeCase(type);
        return $"{ResourceRoot}/{ShunModRelicsPath}/{name}/{name}_outline.png";
    }
}