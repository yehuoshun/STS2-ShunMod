using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Enchantments;

namespace ShunMod.Shun.RestSite;

/// <summary>
///     休息处附魔：选一张牌，施加预选附魔（层数5）。已附魔的牌会被替换或叠加。
/// </summary>
public class EnchantRestSiteOption(Player owner) : RestSiteOption(owner)
{
    /// <summary>
    ///     附魔层数，默认 5 层。
    /// </summary>
    private const int EnchantStacks = 5;

    private const string CustomDescKey = "shunmod_enchant_custom_desc";
    private static bool _localizationAdded;

    /// <summary>
    ///     预选的附魔，在构造时随机决定，显示在选项描述中。
    /// </summary>
    private readonly EnchantmentModel _cachedEnchantment = RollRandomEnchantment(owner);

    public override string OptionId => "ENCHANT";

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
            [CustomDescKey] = "为牌组中的一张牌施加 [gold]{enchantment}：{enchant_desc}[/gold]"
        });
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

        // CardCmd.Enchant 内部自动处理同类型叠加 + 记录历史
        // 部分 mod（如 RepeatableEnchantments）会检查兼容性，不兼容时抛异常，需兜底
        try
        {
            CardCmd.Enchant(_cachedEnchantment.ToMutable(), card, EnchantStacks);
        }
        catch (Exception e)
        {
            Log.Warn($"[EnchantRestSiteOption] 附魔失败：{e.Message}");
            return false;
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