using MegaCrit.Sts2.Core.Entities.Relics;

namespace STS2ShunMod.STS2_ShunModCode.Core;

/// <summary>
///     ShunMod 遗物基类 — 自动提供 PackedIconPath / PackedIconOutlinePath / BigIconPath。
///     子类只需加 [RelicPool] 特性、覆写 Rarity 和实现效果方法。
/// </summary>
public abstract class ShunRelic : RelicModel
{
    public override string PackedIconPath => ShunModHelper.RelicIconPath(GetType());
    protected override string PackedIconOutlinePath => ShunModHelper.RelicOutlinePath(GetType());
    protected override string BigIconPath => ShunModHelper.RelicIconPath(GetType());
}
