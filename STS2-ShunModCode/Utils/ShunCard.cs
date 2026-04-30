using System.Text.RegularExpressions;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace STS2_ShunMod.Utils;

/// <summary>
/// ShunMod 卡牌基类 — 提供链式构造方法和通用默认实现。
/// 所有自定义卡牌继承此类。
/// </summary>
public abstract partial class ShunCard : CardModel
{
    private static readonly Regex CamelCaseRegex = CamelCasePattern();

    private readonly List<CardKeyword> _keywords = [];
    private readonly List<(CardKeyword Keyword, bool Remove)> _upgradeKeywords = [];
    private readonly List<DynamicVar> _vars = [];
    private readonly HashSet<CardTag> _tags = [];
    private readonly List<Func<CardModel, IHoverTip>> _hoverTips = [];

    // --- 自动 ID ---
    protected virtual string CardId => CamelCaseRegex.Replace(GetType().Name, "$1_$2").ToLowerInvariant();

    // --- 卡图路径（默认引用同名 png） ---
    protected virtual string PortraitBasePath => $"res://STS2_ShunMod/images/card_portraits/{CardId}";
    public override string PortraitPath =>
        ResourceLoader.Exists($"{PortraitBasePath}.png") ? $"{PortraitBasePath}.png" : string.Empty;

    // --- 费用升级值 ---
    protected int? CostUpgradeAmount { get; set; }

    // --- 构造 ---
    protected ShunCard(int baseCost, CardType type, CardRarity rarity, TargetType target,
        bool showInCardLibrary = true)
        : base(baseCost, type, rarity, target, showInCardLibrary)
    {
    }

    // --- Canonical 覆写（sealed，子类通过链式方法填充） ---
    protected sealed override IEnumerable<DynamicVar> CanonicalVars => _vars;
    public sealed override IEnumerable<CardKeyword> CanonicalKeywords => _keywords;
    protected sealed override HashSet<CardTag> CanonicalTags => _tags;
    protected sealed override IEnumerable<IHoverTip> ExtraHoverTips =>
        _hoverTips.Select(t => t(this));

    // --- 升级处理 ---
    protected override void OnUpgrade()
    {
        foreach (var (keyword, remove) in _upgradeKeywords)
        {
            if (remove) RemoveKeyword(keyword);
            else AddKeyword(keyword);
        }
        if (CostUpgradeAmount.HasValue)
            EnergyCost.UpgradeBy(CostUpgradeAmount.Value);
    }

    // ═══════════════════════════════════════════
    //  链式构造方法
    // ═══════════════════════════════════════════

    protected ShunCard WithVar(string name, int baseVal, int upgrade = 0)
    {
        _vars.Add(new DynamicVar(name, baseVal).WithUpgrade(upgrade));
        return this;
    }

    protected ShunCard WithDamage(int baseVal, int upgrade = 0)
    {
        _vars.Add(new DamageVar(baseVal, ValueProp.Move).WithUpgrade(upgrade));
        return this;
    }

    protected ShunCard WithBlock(int baseVal, int upgrade = 0)
    {
        _vars.Add(new BlockVar(baseVal, ValueProp.Move).WithUpgrade(upgrade));
        return this;
    }

    protected ShunCard WithPower<T>(int baseVal, int upgrade = 0) where T : PowerModel
    {
        _vars.Add(new PowerVar<T>(baseVal).WithUpgrade(upgrade));
        _hoverTips.Add(_ => HoverTipFactory.FromPower<T>());
        return this;
    }

    protected ShunCard WithKeywords(params CardKeyword[] keywords)
    {
        _keywords.AddRange(keywords);
        return this;
    }

    /// <summary>
    /// 添加关键词，并可选择升级时移除。
    /// </summary>
    protected ShunCard WithKeyword(CardKeyword keyword, bool removeOnUpgrade = false)
    {
        if (!removeOnUpgrade) _keywords.Add(keyword);
        _upgradeKeywords.Add((keyword, removeOnUpgrade));
        return this;
    }

    protected ShunCard WithTags(params CardTag[] tags)
    {
        foreach (var tag in tags) _tags.Add(tag);
        return this;
    }

    protected ShunCard WithTip(CardKeyword keyword)
    {
        _hoverTips.Add(_ => HoverTipFactory.FromKeyword(keyword));
        return this;
    }

    protected ShunCard WithTip<T>() where T : PowerModel
    {
        _hoverTips.Add(_ => HoverTipFactory.FromPower<T>());
        return this;
    }

    protected ShunCard WithCostUpgradeBy(int amount)
    {
        CostUpgradeAmount = amount;
        return this;
    }

    [GeneratedRegex(@"([a-z])([A-Z])", RegexOptions.Compiled)]
    private static partial Regex CamelCasePattern();
}
