using MegaCrit.Sts2.Core.Models;
using ShunMod.Shun.Base;

namespace ShunMod.Shun.Base;

/// <summary>
///     ShunMod 遗物路径工具 — 自动生成 PackedIconPath / PackedIconOutlinePath / BigIconPath。
/// </summary>
public static class ShunRelic
{
    public static string PackedIconPath<T>() where T : class
    {
        return ShunModHelper.RelicIconPath(typeof(T));
    }

    public static string PackedIconOutlinePath<T>() where T : class
    {
        return ShunModHelper.RelicOutlinePath(typeof(T));
    }

    public static string BigIconPath<T>() where T : class
    {
        return ShunModHelper.RelicIconPath(typeof(T));
    }
}

/// <summary>
///     ShunMod 遗物基类 — 自动生成图片路径。
///     继承 ShunRelicModel&lt;T&gt; 即可省去 PackedIconPath / PackedIconOutlinePath / BigIconPath 三个重写。
/// </summary>
public abstract class ShunRelicModel<T> : RelicModel where T : RelicModel
{
    public override string PackedIconPath => ShunRelic.PackedIconPath<T>();
    protected override string PackedIconOutlinePath => ShunRelic.PackedIconOutlinePath<T>();
    protected override string BigIconPath => ShunRelic.BigIconPath<T>();
}