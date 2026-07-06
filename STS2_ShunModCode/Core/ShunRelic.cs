namespace STS2ShunMod.STS2_ShunModCode.Core;

/// <summary>
///     ShunMod 遗物路径工具 — 自动生成 PackedIconPath / PackedIconOutlinePath / BigIconPath。
///     子类在对应属性中调用即可消除重复样板。
/// </summary>
public static class ShunRelic
{
    public static string PackedIconPath<T>() where T : class => ShunModHelper.RelicIconPath(typeof(T));
    public static string PackedIconOutlinePath<T>() where T : class => ShunModHelper.RelicOutlinePath(typeof(T));
    public static string BigIconPath<T>() where T : class => ShunModHelper.RelicIconPath(typeof(T));
}
