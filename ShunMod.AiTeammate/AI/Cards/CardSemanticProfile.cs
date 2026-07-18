using System.Collections.Generic;

namespace ShunMod.AiTeammate;

internal sealed class CardSemanticProfile
{
    public IReadOnlyList<NormalizedEffectDescriptor> Effects { get; init; } = [];
}
