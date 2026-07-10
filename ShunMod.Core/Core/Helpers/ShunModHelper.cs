using System.Text.RegularExpressions;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace ShunMod.Core.Core.Helpers;

/// <summary>
///     ShunMod 工具类 — 资源路径推导、遗物安全访问等通用方法。
/// </summary>
public static class ShunModHelper
{
    private const string ResourceRoot = "res://ShunMod_Shun/images";
    private const string ShunModCardsPath = "cards/shunCards";
    private const string ShunModRelicsPath = "relics/shunRelics";
    private const string ShunModEventsPath = "events/shunEvents";

    // PascalCase → snake_case 编译正则（static readonly 实例化一次，RegexOptions.Compiled 加速匹配）
    private static readonly Regex PascalToSnake = new("([a-z])([A-Z])", RegexOptions.Compiled);

    /// <summary>类名 → snake_case（如 ShunModSuperApotheosis → shun_mod_super_apotheosis）</summary>
    public static string ClassToSnakeCase(Type type)
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

    /// <summary>事件图片路径：events/shunEvents/{snake}.png</summary>
    public static string EventImagePath(Type type)
    {
        return $"{ResourceRoot}/{ShunModEventsPath}/{ClassToSnakeCase(type)}.png";
    }

    // 未注册 Pool 的遗物直接访问 HoverTips 会抛 InvalidOperationException，吞掉防 UI 崩
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

    // 自定义事件 GetAssetPaths() 样板提取，替换默认路径为 mod 图片路径
    /// <summary>替换事件默认图片路径为 mod 自定义图片路径。</summary>
    public static IEnumerable<string> ReplaceEventImage(
        IEnumerable<string> paths, Type eventType, string entry)
    {
        var list = paths.ToList();
        var defaultPath = ImageHelper.GetImagePath($"events/{entry.ToLowerInvariant()}.png");
        var modPath = EventImagePath(eventType);
        var i = list.IndexOf(defaultPath);
        if (i >= 0) list[i] = modPath;
        else list.Add(modPath);
        return list;
    }
}