using System.Diagnostics.CodeAnalysis;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;

namespace ShunMod.Tweaks.Patches.Overflow;

/// <summary>
///     超限生命 —「命银行」层，1 层 = 一条命。
///     入账：生命上限顶格后继续增加（SetMaxHpInternal 被 clamp 吞掉的部分）、
///     满血时的溢出治疗，按「每管血（=当前 MaxHp）存一条命」折算。
///     出账：受到致命伤害时自动消耗一条命并回满血。
///     生命本身跨战斗保留，因此本 Power 也跨战斗持久化（不在 PowersPersist 黑名单）。
/// </summary>
[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
public sealed class OverflowLifePower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
}