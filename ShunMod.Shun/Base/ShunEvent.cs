using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using ShunMod.Shun.Helpers;

namespace ShunMod.Shun.Base;

/// <summary>
///     ShunMod 事件基类 — 自动替换默认图片路径为 mod 自定义图片路径。
///     继承 ShunEventModel 即可省去 GetAssetPaths 重写。
/// </summary>
public abstract class ShunEventModel : EventModel
{
    public override IEnumerable<string> GetAssetPaths(IRunState runState)
        => ShunModHelper.ReplaceEventImage(base.GetAssetPaths(runState), GetType(), Id.Entry);
}