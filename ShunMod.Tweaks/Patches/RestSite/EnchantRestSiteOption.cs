using System.Collections.Generic;
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

    private const string CustomDescKey = "shunmod_enchant_custom_desc";
    private static bool _customEntryAdded;

    /// <summary>
    ///     在本地化表中注入自定义描述模板（仅一次）。
    /// </summary>
    private static void EnsureCustomDescriptionEntry()
    {
        if (_customEntryAdded) return;
        _customEntryAdded = true;

        var table = LocManager.Instance.GetTable("rest_site_ui");
        if (!table.HasEntry(CustomDescKey))
        {
            table.MergeWith(new Dictionary<string, string>
            {
                [CustomDescKey] = "为牌组中的一张牌施加 {enchantment}：{enchant_desc}"
            });
        }
    }

    /// <summary>
    ///     选项描述：原动作文本 + 附魔名 + 附魔描述。
    /// </summary>
    public override LocString Description
    {
        get
        {
            EnsureCustomDescriptionEntry();
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

        var selected = await CardSelectCmd.FromDeckGeneric(
            Owner,
            prefs,
            card => card.Type != CardType.Curse
                    && card.Type != CardType.Status);

        if (!selected.Any())
            return false;

        var card = selected.First();

        // 直接使用预选附魔，不检查兼容性
        CardCmd.Enchant(_cachedEnchantment.ToMutable(), card, EnchantStacks);

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