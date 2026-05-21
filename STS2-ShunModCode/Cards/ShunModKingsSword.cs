using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using STS2_ShunMod.Core;
using STS2_ShunMod.Core.Registration;

namespace STS2_ShunMod.Cards;

/// <summary>
///     君王之剑 — 唯一神兵。
///     不可复制、不可消耗。
///     每当打出一张牌时，若此卡存在于任意牌堆中，则抽到此卡。
/// </summary>
[Pool(typeof(ColorlessCardPool))]
public class ShunModKingsSword : ShunCard
{
    public ShunModKingsSword()
        : base(1, CardType.Skill, CardRarity.Event, TargetType.Self, showInLibrary: false)
    {
    }

    /// <summary>君王之剑打出无特殊效果——其力量来自全局补丁。</summary>
    protected override Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        return Task.CompletedTask;
    }

    public override string PortraitPath =>
        "res://STS2-ShunMod/images/packed/card_portraits/colorless/kings_sword.png";
}