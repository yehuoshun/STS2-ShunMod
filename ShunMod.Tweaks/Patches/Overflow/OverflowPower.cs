using System.Diagnostics.CodeAnalysis;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;

namespace ShunMod.Tweaks.Patches.Overflow;

/// <summary>
///     超限 — 护盾溢出记账层，1 层 = <see cref="OverflowConfig.Threshold"/> 护盾。
///     本质仍然是护盾：受伤时护盾不足会自动拆层抵扣，格挡归零时一同清零；
///     唯一区别：不可被「移除增益」类效果清除（PowerCmd.Remove 补丁兜底）。
/// </summary>
[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
public sealed class OverflowPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
}