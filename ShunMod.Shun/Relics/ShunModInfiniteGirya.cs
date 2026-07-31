using System.Diagnostics.CodeAnalysis;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using ShunMod.Core.Core.Registry;
using ShunMod.Core.Base;

// ReSharper disable UnusedType.Global — 游戏框架反射使用
// ReSharper disable UnusedType.Instantiation — 游戏框架反射实例化
namespace ShunMod.Shun.Relics;

/// <summary>
///     无限壶铃 — 基于原版 Girya，去掉 maxLifts=3 限制，休息处无限举重获得力量。
/// </summary>
[RelicPool(typeof(SharedRelicPool))]
[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
public sealed class ShunModInfiniteGirya : ShunRelicModel<ShunModInfiniteGirya>
{
    private int _timesLifted;

    public override RelicRarity Rarity => RelicRarity.Rare;

    public override bool ShowCounter => true;

    public override int DisplayAmount => TimesLifted;

    [SavedProperty]
    public int TimesLifted
    {
        get => _timesLifted;
        set
        {
            AssertMutable();
            _timesLifted = value;
            InvokeDisplayAmountChanged();
        }
    }

    public override bool IsAllowed(IRunState runState)
    {
        return IsBeforeAct3TreasureChest(runState);
    }

    public override async Task AfterRoomEntered(AbstractRoom room)
    {
        if (TimesLifted > 0 && room is CombatRoom)
        {
            Flash();
            await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), Owner.Creature, TimesLifted,
                Owner.Creature, null);
        }
    }

    public override bool TryModifyRestSiteOptions(Player player, ICollection<RestSiteOption> options)
    {
        if (player != Owner) return false;
        // 无限版：不检查 maxLifts，始终可举
        options.Add(new ShunModLiftRestSiteOption(player));
        return true;
    }
}

/// <summary>
///     无限壶铃的休息处举重选项。基于原版 LiftRestSiteOption，但绑定 ShunModInfiniteGirya。
/// </summary>
public class ShunModLiftRestSiteOption(Player owner) : RestSiteOption(owner)
{
    public override string OptionId => "LIFT";

    public override LocString Description
    {
        get
        {
            var desc = base.Description;
            var relic = Owner.GetRelic<ShunModInfiniteGirya>();
            desc.Add("LiftsLeft", relic != null ? relic.TimesLifted + 1 : 1);
            return desc;
        }
    }

    public override Task<bool> OnSelect()
    {
        var relic = Owner.GetRelic<ShunModInfiniteGirya>();
        if (relic != null)
            relic.TimesLifted++;
        return Task.FromResult(true);
    }

    public override Task DoLocalPostSelectVfx(CancellationToken ct = default)
    {
        NGame.Instance?.ScreenShake(ShakeStrength.Strong, ShakeDuration.Short);
        return Task.CompletedTask;
    }
}