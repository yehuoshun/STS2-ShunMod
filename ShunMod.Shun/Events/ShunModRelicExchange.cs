using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using ShunMod.Core;
using ShunMod.Shun.UI;

namespace ShunMod.Shun.Events;

/// <summary>
///     遗物交易所 — 自选模式。
///     使用 Overlay Screen 实现完整 UI。进入事件后弹出选屏，玩家选择要卖掉的遗物和要获得的奖励。
/// </summary>
[EventPool]
public class ShunModRelicExchange : ShunEventModel
{
    // ═══════════════════════════════════════════════════════════
    //  静态数据
    // ═══════════════════════════════════════════════════════════

    private static readonly HashSet<RelicRarity> TradeableRarities =
        [RelicRarity.Common, RelicRarity.Uncommon, RelicRarity.Rare, RelicRarity.Shop, RelicRarity.None];

    private static readonly HashSet<string> EnchantBlacklist = new()
    {
        "Adroit", "PerfectFit", "RoyallyApproved", "SlumberingEssence",
        "Sown", "Spiral", "Steady", "TezcatarasEmber", "Vigorous",
        "Swift", "Glam", "Clone", "Goopy", "Momentum", "Inky",
    };

    // ═══════════════════════════════════════════════════════════
    //  入口
    // ═══════════════════════════════════════════════════════════

    public ShunModRelicExchange() : base()
    {
    }

    protected override IEnumerable<DynamicVar> CanonicalVars => [];
    public override void CalculateVars() { }

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        var player = Owner;

        return new List<EventOption>
        {
            new(this, async () =>
            {
                if (player == null) return;
                var tradeable = player.Relics.Where(IsTradeable).ToList();
                if (tradeable.Count == 0)
                {
                    // 没有可交易的遗物
                    SetEventState(L10NLookup("pages.NO_TRADEABLE.description"),
                    [
                        new EventOption(this, () => { }, "OPT_LEAVE")
                    ]);
                    return;
                }

                // 弹出 Overlay 屏幕
                await ShunModRelicExchangeCoordinator.ShowExchange(player);
            }, $"{Id.Entry}.pages.INITIAL.options.OPT_ENTER.title",
                BuildEnterHoverTips()),

            new(this, async () =>
            {
                // 扣血刷新（保留旧版功能）
                if (player == null) return;
                if (player.CurrentHealth > 5)
                {
                    player.Creature.LoseHpInternal(5, 0);
                    Log.Info("[ShunMod_Shun] RelicExchange: HP refresh, options will re-roll on next entry");
                }
            }, $"{Id.Entry}.pages.INITIAL.options.OPT_REFRESH.title",
                $"{Id.Entry}.pages.INITIAL.options.OPT_REFRESH.description"),

            new(this, async () =>
            {
                // 离开
            }, $"{Id.Entry}.pages.INITIAL.options.OPT_LEAVE.title",
                $"{Id.Entry}.pages.INITIAL.options.OPT_LEAVE.description"),
        };
    }

    private List<IHoverTip> BuildEnterHoverTips()
    {
        var player = Owner;
        if (player == null) return [];

        var tips = new List<IHoverTip>();
        var tradeable = player.Relics.Where(IsTradeable).ToList();
        foreach (var relic in tradeable)
        {
            tips.AddRange(ShunModHelper.SafeRelicHoverTips(relic));
        }
        return tips;
    }

    private static bool IsTradeable(RelicModel r) => TradeableRarities.Contains(r.Rarity);
}