using System.Text.RegularExpressions;

namespace STS2ShunMod.STS2_ShunModCode.Core;

/// <summary>
///     ShunMod 资源路径工具 — 从类名自动推导图片路径。
/// </summary>
public static class ShunModHelper
{
    private const string ResourceRoot = "res://STS2_ShunMod/images";

    /// <summary>类名 → snake_case（如 ShunModSuperApotheosis → shun_mod_super_apotheosis）</summary>
    public static string ClassToSnakeCase(Type type)
    {
        return ClassNameToSnakeCase(type.Name);
    }

    /// <summary>类名字符串 → snake_case</summary>
    public static string ClassNameToSnakeCase(string className)
    {
        return Regex.Replace(className, "([a-z])([A-Z])", "$1_$2").ToLowerInvariant();
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