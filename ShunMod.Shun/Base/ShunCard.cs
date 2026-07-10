using ShunMod.Shun.Helpers;

namespace ShunMod.Shun.Base;

/// <summary>
///     ShunMod 卡牌路径工具 — 自动生成 PortraitPath。
///     子类在对应属性中调用即可消除重复样板。
///     非无色卡牌可传 color 参数。
/// </summary>
public static class ShunCard
{
    public static string PortraitPath<T>(string color = "colorless") where T : class
        => ShunModHelper.CardPortraitPath(typeof(T), color);
}
