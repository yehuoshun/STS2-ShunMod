using MegaCrit.Sts2.Core.Models;

namespace STS2ShunMod.STS2_ShunModCode.Core;

/// <summary>
///     ShunMod 卡牌基类 — 自动提供 PortraitPath。
///     子类只需加 [CardPool] 特性，实现 OnPlay 和升级逻辑。
///     非无色卡牌可覆写 CardColor 属性。
/// </summary>
public abstract class ShunCard : CardModel
{
    protected ShunCard(int baseCost, CardType type, CardRarity rarity, TargetType target)
        : base(baseCost, type, rarity, target) { }

    /// <summary>卡牌颜色，用于资源路径。默认 colorless。</summary>
    protected virtual string CardColor => "colorless";

    public override string PortraitPath => ShunModHelper.CardPortraitPath(GetType(), CardColor);
}
