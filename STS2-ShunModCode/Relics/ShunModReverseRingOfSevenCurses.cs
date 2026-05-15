using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using STS2_ShunMod.Core.Registration;

namespace STS2_ShunMod.Relics;

/// <summary>
/// 逆七咒之戒 — 七咒之戒的反转版。
/// 7 道诅咒逆转为 7 道祝福。
/// </summary>
[Pool(typeof(SharedRelicPool))]
public class ShunModReverseRingOfSevenCurses : RelicModel
{
    private bool _goldGuard;
    private decimal _lastGoldGain;

    // ═══════════════════════════════════════════
    // 基础属性
    // ═══════════════════════════════════════════

    public override RelicRarity Rarity => RelicRarity.Event;

    protected override IEnumerable<DynamicVar> CanonicalVars => [];

    protected override string BigIconPath =>
        "res://STS2_ShunMod/images/relics/reverse_ring_of_seven_curses.png";

    public override string PackedIconPath =>
        "res://STS2_ShunMod/images/relics/reverse_ring_of_seven_curses.png";

    // ═══════════════════════════════════════════
    // 7 诅咒 → 7 祝福
    // ═══════════════════════════════════════════

    /// <summary>
    /// 1. 受到伤害 -50%
    /// 2. 造成伤害 +25%
    /// </summary>
    public override decimal ModifyDamageMultiplicative(
        Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource)
    {
        if (target != null && target.Player == Owner)
            return 0.5m;

        if (dealer != null && dealer.Player == Owner)
            return 1.25m;

        return 1m;
    }

    /// <summary>3. 获得格挡 +20%</summary>
    public override decimal ModifyBlockMultiplicative(
        Creature target, decimal block, ValueProp props,
        CardModel? cardSource, CardPlay? cardPlay)
    {
        if (target.Player == Owner)
            return 1.2m;
        return 1m;
    }

    /// <summary>4. 获得金币 +50%（防递归标志）</summary>
    public override bool ShouldGainGold(decimal amount, Player player)
    {
        if (!_goldGuard && player == Owner)
            _lastGoldGain = amount;
        return true;
    }

    public override async Task AfterGoldGained(Player player)
    {
        if (_goldGuard || player != Owner || Owner == null)
            return;

        _goldGuard = true;
        int extraGold = (int)Math.Floor(_lastGoldGain * 0.5m);
        if (extraGold > 0)
            await PlayerCmd.GainGold(extraGold, Owner);
        _goldGuard = false;
    }

    /// <summary>5. 休息处回复血量 +25%</summary>
    public override decimal ModifyRestSiteHealAmount(Creature creature, decimal amount)
    {
        if (creature.Player == Owner || creature.PetOwner == Owner)
            return amount * 1.25m;
        return amount;
    }

    public override IReadOnlyList<LocString> ModifyExtraRestSiteHealText(
        Player player, IReadOnlyList<LocString> currentExtraText)
    {
        if (player != Owner)
            return currentExtraText;

        var list = new List<LocString>(currentExtraText);
        var extraText = new LocString(
            "relics",
            "ShunModReverseRingOfSevenCurses.additionalRestSiteHealText");
        decimal baseHeal = (decimal)player.Creature.MaxHp * 0.3m;
        decimal actualHeal = baseHeal * 1.5m;
        int actualHealInt = (int)actualHeal;
        extraText.Add("ActualHeal", actualHealInt.ToString());
        list.Add(extraText);
        return list;
    }

    /// <summary>6. 休息处升级卡牌 +1</summary>
    public override bool TryModifyRestSiteOptions(
        Player player, ICollection<RestSiteOption> options)
    {
        if (player != Owner)
            return false;

        var smithOptions = options.OfType<SmithRestSiteOption>().ToList();
        if (smithOptions.Count > 0)
        {
            smithOptions[0].SmithCount += 1;
            return true;
        }
        return false;
    }

    // ═══════════════════════════════════════════
    // 7 祝福 → 诅咒
    // ═══════════════════════════════════════════

    /// <summary>7. 抽牌 -1（原祝福 +1 逆转）</summary>
    public override decimal ModifyHandDraw(Player player, decimal count)
    {
        if (player == Owner)
            return Math.Max(0, count - 1);
        return count;
    }
}
