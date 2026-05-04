using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace STS2_ShunMod.Utils;

/// <summary>
/// 卡牌基类 — 封装 CardModel 的抽象成员，提供链式配置方法。
/// </summary>
/// <remarks>
/// 所有自定义卡牌需继承此类，仅重写构造函数与 OnPlay 即可。
/// 链式配置方法（WithKeywords / WithTip / WithCostUpgradeBy）在构造函数中调用。
/// </remarks>
public abstract class ShunCard : CardModel
{
    /// <summary>
    /// 卡牌关键词列表（消耗 / 保留 / 虚无等）。
    /// </summary>
    private readonly List<CardKeyword> _keywords = [];

    /// <summary>
    /// 悬停提示工厂列表，用于生成关键词对应的鼠标悬浮说明。
    /// </summary>
    private readonly List<Func<CardModel, IHoverTip>> _hoverTips = [];

    /// <summary>
    /// 升级后费用变化量（如 -1 表示升级后减 1 费）。
    /// null 表示升级不改变费用。
    /// </summary>
    private int? _costUpgrade;

    /// <summary>
    /// 构造卡牌基础属性，透传至游戏 CardModel。
    /// </summary>
    /// <param name="baseCost">基础费用</param>
    /// <param name="type">卡牌类型（攻击 / 技能 / 能力）</param>
    /// <param name="rarity">稀有度</param>
    /// <param name="target">目标类型</param>
    protected ShunCard(int baseCost, CardType type, CardRarity rarity, TargetType target)
        : base(baseCost, type, rarity, target)
    {
    }

    // ════════════════════════════════════════════════════════
    // 抽象成员实现（sealed 防止子类覆盖，统一由基类管理）
    // ════════════════════════════════════════════════════════

    /// <summary>
    /// 卡牌动态变量（伤害 / 格挡数值等），当前为空，按需扩展。
    /// </summary>
    protected sealed override IEnumerable<DynamicVar> CanonicalVars => [];

    /// <summary>
    /// 卡牌关键词，由 WithKeywords 填充。
    /// </summary>
    public sealed override IEnumerable<CardKeyword> CanonicalKeywords => _keywords;

    /// <summary>
    /// 卡牌标签（打击 / 防御等），当前为空。
    /// </summary>
    protected sealed override HashSet<CardTag> CanonicalTags => [];

    /// <summary>
    /// 额外悬停提示，由 WithTip 填充。
    /// </summary>
    protected sealed override IEnumerable<IHoverTip> ExtraHoverTips =>
        _hoverTips.Select(t => t(this));

    // ════════════════════════════════════════════════════════
    // 升级回调
    // ════════════════════════════════════════════════════════

    /// <summary>
    /// 升级时应用费用变化（如果有配置）。
    /// </summary>
    protected override void OnUpgrade()
    {
        if (_costUpgrade.HasValue)
            EnergyCost.UpgradeBy(_costUpgrade.Value);
    }

    // ════════════════════════════════════════════════════════
    // 链式配置方法（在子类构造函数中调用）
    // ════════════════════════════════════════════════════════

    /// <summary>
    /// 添加卡牌关键词。
    /// </summary>
    /// <param name="keywords">关键词列表</param>
    protected void WithKeywords(params CardKeyword[] keywords)
    {
        _keywords.AddRange(keywords);
    }

    /// <summary>
    /// 添加关键词对应的悬停提示。
    /// </summary>
    /// <param name="keyword">关键词</param>
    protected void WithTip(CardKeyword keyword)
    {
        _hoverTips.Add(_ => HoverTipFactory.FromKeyword(keyword));
    }

    /// <summary>
    /// 设置升级后费用变化。
    /// </summary>
    /// <param name="amount">费用变化量（负数 = 减费）</param>
    protected void WithCostUpgradeBy(int amount)
    {
        _costUpgrade = amount;
    }
}
