using System;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using ShunMod.Shun.Relics;

namespace ShunMod.Shun.Patches;

/// <summary>
///     生生不息右键 GameAction — 走 game action queue 执行，
///     确保 CardSelectCmd 能正确获取 PlayerChoiceContext。
///     （单机限定，不支持多人同步）
/// </summary>
public sealed class EndlessLifeRightClickAction : GameAction
{
    public override ulong OwnerId => Player.NetId;

    public override GameActionType ActionType => GameActionType.CombatPlayPhaseOnly;

    public override bool RecordableToReplay => false;

    private Player Player { get; }
    private ShunModEndlessLife Relic { get; }

    public EndlessLifeRightClickAction(Player player, ShunModEndlessLife relic)
    {
        Player = player;
        Relic = relic;
    }

    protected override async Task ExecuteAction()
    {
        var choiceContext = new GameActionPlayerChoiceContext(this);
        await Relic.ExecuteRightClick(choiceContext);
    }

    /// <summary>
    ///     单机遗物，不支持多人同步。
    /// </summary>
    public override INetAction ToNetAction()
    {
        throw new NotSupportedException("EndlessLifeRightClickAction 不支持多人同步");
    }
}