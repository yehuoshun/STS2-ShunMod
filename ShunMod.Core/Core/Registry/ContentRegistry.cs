using System.Reflection;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;

namespace ShunMod.Core.Core.Registry;

/// <summary>
///     内容自动注册器 — 扫描 [CardPool] / [RelicPool] 特性并注册。
///     约定：一个类只能标记一种 Pool 类型，多重标记会被跳过只注册第一个。
/// </summary>
public static class ContentRegistry
{
    /// <summary>
    ///     扫描程序集中所有非抽象类，按特性类型分别注册。
    /// </summary>
    public static void RegisterAll(Assembly assembly)
    {
        var cardCount = 0;
        var relicCount = 0;

        foreach (var type in assembly.GetTypes())
        {
            if (type.IsAbstract) continue;

            var cardAttr = type.GetCustomAttribute<CardPoolAttribute>();
            if (cardAttr != null)
            {
                ModelDb.Inject(type);
                ModHelper.AddModelToPool(cardAttr.PoolType, type);
                cardCount++;
                continue;
            }

            var relicAttr = type.GetCustomAttribute<RelicPoolAttribute>();
            if (relicAttr == null) continue;
            ModelDb.Inject(type);
            ModHelper.AddModelToPool(relicAttr.PoolType, type);
            relicCount++;
        }

        Log.Info($"[ShunMod_Core] ContentRegistry: {cardCount} cards, {relicCount} relics");
    }
}