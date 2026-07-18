using MegaCrit.Sts2.Core.Models;

namespace ShunMod.AiTeammate;

internal interface ICardResolver
{
    ResolvedCardView Resolve(CardModel liveCard, string cardInstanceId);
}
