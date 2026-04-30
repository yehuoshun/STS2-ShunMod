using System.Text.RegularExpressions;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace STS2_ShunMod.Utils;

/// <summary>
/// ShunMod 卡牌基类。
/// </summary>
public abstract partial class ShunCard : CardModel
{
    private static readonly Regex CamelCaseRegex = CamelCasePattern();

    private readonly List<CardKeyword> _keywords = [];
    private readonly List<(CardKeyword Keyword, bool Remove)> _upgradeKeywords = [];
    private readonly List<DynamicVar> _vars = [];
    private readonly HashSet<CardTag> _tags = [];
    private readonly List<Func<CardModel, IHoverTip>> _hoverTips = [];

    protected int? CostUpgradeAmount { get; set; }

    protected virtual string CardId => CamelCaseRegex.Replace(GetType().Name, "$1_$2").ToLowerInvariant();

    public override string PortraitPath =>
        ResourceLoader.Exists(PortraitPngPath) ? PortraitPngPath : string.Empty;
    protected virtual string PortraitPngPath => $"res://STS2_ShunMod/images/card_portraits/{CardId}.png";

    protected ShunCard(int baseCost, CardType type, CardRarity rarity, TargetType target,
        bool showInCardLibrary = true)
        : base(baseCost, type, rarity, target, showInCardLibrary)
    {
    }

    protected sealed override IEnumerable<DynamicVar> CanonicalVars => _vars;
    public sealed override IEnumerable<CardKeyword> CanonicalKeywords => _keywords;
    protected sealed override HashSet<CardTag> CanonicalTags => _tags;
    protected sealed override IEnumerable<IHoverTip> ExtraHoverTips =>
        _hoverTips.Select(t => t(this));

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

    // ═══ 链式方法 ═══

    protected ShunCard WithVar(string name, int baseVal)
    {
        _vars.Add(new DynamicVar(name, baseVal));
        return this;
    }

    protected ShunCard WithKeywords(params CardKeyword[] keywords)
    {
        _keywords.AddRange(keywords);
        return this;
    }

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

    protected ShunCard WithCostUpgradeBy(int amount)
    {
        CostUpgradeAmount = amount;
        return this;
    }

    [GeneratedRegex(@"([a-z])([A-Z])", RegexOptions.Compiled)]
    private static partial Regex CamelCasePattern();
}
