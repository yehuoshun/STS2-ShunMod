using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace STS2_ShunMod.Utils;

public abstract class ShunCard : CardModel
{
    private readonly List<CardKeyword> _keywords = [];
    private readonly List<Func<CardModel, IHoverTip>> _hoverTips = [];

    private int? _costUpgrade;

    protected ShunCard(int baseCost, CardType type, CardRarity rarity, TargetType target)
        : base(baseCost, type, rarity, target)
    {
    }

    protected sealed override IEnumerable<DynamicVar> CanonicalVars => [];
    public sealed override IEnumerable<CardKeyword> CanonicalKeywords => _keywords;
    protected sealed override HashSet<CardTag> CanonicalTags => [];
    protected sealed override IEnumerable<IHoverTip> ExtraHoverTips =>
        _hoverTips.Select(t => t(this));

    protected override void OnUpgrade()
    {
        if (_costUpgrade.HasValue)
            EnergyCost.UpgradeBy(_costUpgrade.Value);
    }

    protected void WithKeywords(params CardKeyword[] keywords)
    {
        _keywords.AddRange(keywords);
    }

    protected void WithTip(CardKeyword keyword)
    {
        _hoverTips.Add(_ => HoverTipFactory.FromKeyword(keyword));
    }

    protected void WithCostUpgradeBy(int amount)
    {
        _costUpgrade = amount;
    }
}
