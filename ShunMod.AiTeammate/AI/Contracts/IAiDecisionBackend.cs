using System.Threading;
using System.Threading.Tasks;

namespace ShunMod.AiTeammate;

internal interface IAiDecisionBackend
{
    Task<AiDecisionResult> DecideAsync(AiDecisionRequest request, CancellationToken ct);
}