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
///     休息处附魔：选一张牌，施加预选附魔（层数5）。已附魔的牌会被替换或叠加。
/// </summary>
public class EnchantRestSiteOption : RestSiteOption
{
    public override string OptionId => "ENCHANT";

    /// <summary>
    ///     预选的附魔，在构造时随机决定，显示在选项描述中。
    /// </summary>
    private readonly EnchantmentModel _cachedEnchantment;

    /// <summary>
    ///     附魔层数，默认 5 层。
    /// </summary>
    private const int EnchantStacks = 5;

    public EnchantRestSiteOption(Player owner) : base(owner)
    {
        _cachedEnchantment = RollRandomEnchantment(owner);
    }

    /// <summary>
    ///     选项描述显示预选的附魔名称。
    /// </summary>
    public override LocString Description => _cachedEnchantment.Title;

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

        // 找所有可用于此牌的附魔（排除已废弃）
        var enchantments = ModelDb.DebugEnchantments
            .Where(e => e is not DeprecatedEnchantment
                        && e.CanEnchantCardType(card.Type)
                        && !card.Keywords.Contains(CardKeyword.Unplayable))
            .ToList();

        if (enchantments.Count == 0)
            return false;

        // 优先使用预选附魔，若不可用则重新随机
        var toApply = enchantments.Any(e => e.Id == _cachedEnchantment.Id)
            ? _cachedEnchantment
            : Owner.PlayerRng.Transformations.NextItem(enchantments);

        CardCmd.Enchant(toApply.ToMutable(), card, EnchantStacks);

        return true;
    }

    /// <summary>
    ///     从所有可用附魔中随机预选一个（不依赖卡牌类型）。
    /// </summary>
    private static EnchantmentModel RollRandomEnchantment(Player owner)
    {
        var pool = ModelDb.DebugEnchantments
            .Where(e => e is not DeprecatedEnchantment)
            .ToList();

        return pool.Count > 0
            ? owner.PlayerRng.Transformations.NextItem(pool)!
            : null!;
    }
}