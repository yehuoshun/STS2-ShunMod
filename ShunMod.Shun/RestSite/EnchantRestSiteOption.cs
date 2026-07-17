using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Enchantments;

namespace ShunMod.Shun.RestSite;

/// <summary>
///     休息处附魔：选一张牌，施加预选附魔（层数5）。已附魔的牌会被替换或叠加。
/// </summary>
public class EnchantRestSiteOption(Player owner) : RestSiteOption(owner)
{
    public override string OptionId => "ENCHANT";

    /// <summary>
    ///     预选的附魔，在构造时随机决定，显示在选项描述中。
    /// </summary>
    private readonly EnchantmentModel _cachedEnchantment = RollRandomEnchantment(owner);

    /// <summary>
    ///     附魔层数，默认 5 层。
    /// </summary>
    private const int EnchantStacks = 5;

    private const string CustomDescKey = "shunmod_enchant_custom_desc";
    private static bool _localizationAdded;

    /// <summary>
    ///     在本地化表中注入缺失的 ENCHANT 选项条目（仅一次）。
    /// </summary>
    private static void EnsureLocalizationEntries()
    {
        if (_localizationAdded) return;
        _localizationAdded = true;

        var table = LocManager.Instance.GetTable("rest_site_ui");
        table.MergeWith(new Dictionary<string, string>
        {
            ["OPTION_ENCHANT.name"] = "附魔",
            ["OPTION_ENCHANT.prompt"] = "选择要附魔的牌",
            [CustomDescKey] = "为牌组中的一张牌施加 [gold]{enchantment}：{enchant_desc}[/gold]"
        });
    }

    /// <summary>
    ///     选项描述：原动作文本 + 附魔名 + 附魔描述。
    /// </summary>
    public override LocString Description
    {
        get
        {
            EnsureLocalizationEntries();
            var desc = new LocString("rest_site_ui", CustomDescKey);
            desc.Add("enchantment", _cachedEnchantment.Title.GetFormattedText());
            desc.Add("enchant_desc", _cachedEnchantment.DynamicDescription.GetFormattedText());
            return desc;
        }
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

        var cards = (await CardSelectCmd.FromDeckGeneric(
            Owner,
            prefs,
            card => card.Type != CardType.Curse
                    && card.Type != CardType.Status)).ToList();

        if (cards.Count == 0)
            return false;

        var card = cards[0];

        // 手动处理：已有同类型附魔则叠加，否则直接附魔
        var enchantment = _cachedEnchantment.ToMutable();
        if (card.Enchantment?.GetType() == enchantment.GetType())
        {
            card.Enchantment.Amount += EnchantStacks;
        }
        else
        {
            CardCmd.Enchant(enchantment, card, EnchantStacks);
        }

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