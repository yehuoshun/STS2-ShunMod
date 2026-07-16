using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Enchantments;

namespace ShunMod.Tweaks.Patches.RestSite;

/// <summary>
///     休息处附魔：选一张牌，随机施加一个可用的附魔。已有附魔的牌会被替换。
/// </summary>
public class EnchantRestSiteOption : RestSiteOption
{
    public override string OptionId => "ENCHANT";

    public EnchantRestSiteOption(Player owner) : base(owner)
    {
    }

    public override async Task<bool> OnSelect()
    {
        // 从牌组选一张牌（排除状态/诅咒），已有附魔也能选
        var prefs = new CardSelectorPrefs(
            new LocString("rest_site_ui", "OPTION_ENCHANT.prompt"),
            1, 1)
        {
            Cancelable = true
        };

        var selected = await CardSelectCmd.FromDeckGeneric(
            Owner,
            prefs,
            card => card.Type != CardType.Curse
                    && card.Type != CardType.Status);

        if (!selected.Any())
            return false;

        var card = selected.First();

        // 找所有可用于此牌的附魔
        var enchantments = ModelDb.DebugEnchantments
            .Where(e => e is not DeprecatedEnchantment
                        && e.CanEnchantCardType(card.Type)
                        && !card.Keywords.Contains(CardKeyword.Unplayable))
            .ToList();

        if (enchantments.Count == 0)
            return false;

        var canonical = Owner.PlayerRng.Transformations.NextItem(enchantments);
        CardCmd.Enchant(canonical.ToMutable(), card, 1);

        return true;
    }
}
