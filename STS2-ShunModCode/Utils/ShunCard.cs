using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace STS2_ShunMod.Utils;

/// <summary>
/// 卡牌基类 — 封装 CardModel 的抽象成员，提供链式配置方法。
/// 所有自定义卡牌继承此类，只重写构造 + OnPlay 即可。
/// </summary>
public abstract class ShunCard : CardModel
{
    // ---- 内部存储（链式方法填充） ----

    private readonly List<CardKeyword> _keywords = [];
    private readonly List<Func<CardModel, IHoverTip>> _hoverTips = [];
    private int? _costUpgrade;

    // ---- 构造，直接透传给游戏 CardModel ----

    protected ShunCard(int baseCost, CardType type, CardRarity rarity, TargetType target)
        : base(baseCost, type, rarity, target)
    {
    }

    // ═══════════════════════════════════════════
    //  CardModel 要求实现的抽象成员（sealed = 子类不再管它们）
    // ═══════════════════════════════════════════

    // 卡牌变量（伤害/格挡数值等），目前空，需要时再加
    protected sealed override IEnumerable<DynamicVar> CanonicalVars => [];

    // 关键词列表（消耗/保留/虚无等），WithKeywords 往里填
    public sealed override IEnumerable<CardKeyword> CanonicalKeywords => _keywords;

    // 标签（打击/防御等），目前空
    protected sealed override HashSet<CardTag> CanonicalTags => [];

    // 悬停提示（鼠标移上去显示的说明），WithTip 往里填
    protected sealed override IEnumerable<IHoverTip> ExtraHoverTips =>
        _hoverTips.Select(t => t(this));

    // ---- 升级回调 ----

    protected override void OnUpgrade()
    {
        if (_costUpgrade.HasValue)
            EnergyCost.UpgradeBy(_costUpgrade.Value);
    }

    // ═══════════════════════════════════════════
    //  链式配置方法（在子类构造函数中调用）
    // ═══════════════════════════════════════════

    /// <summary>添加关键词，如 WithKeywords(CardKeyword.Exhaust)</summary>
    protected void WithKeywords(params CardKeyword[] keywords)
    {
        _keywords.AddRange(keywords);
    }

    /// <summary>添加悬停提示，通常跟关键词对应</summary>
    protected void WithTip(CardKeyword keyword)
    {
        _hoverTips.Add(_ => HoverTipFactory.FromKeyword(keyword));
    }

    /// <summary>升级时费用变化量，如 WithCostUpgradeBy(-1) = 升级后-1费</summary>
    protected void WithCostUpgradeBy(int amount)
    {
        _costUpgrade = amount;
    }
}
