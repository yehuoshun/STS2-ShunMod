namespace ShunMod.AiTeammate;

internal static class AiDecisionBackendFactory
{
    public static IAiDecisionBackend CreateDefault()
    {
        return new DeterministicCombatDecisionBackend(new DeterministicDecisionBackend());
    }
}
