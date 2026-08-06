using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;

// ReSharper disable UnusedMember.Local — Harmony 反射调用
// ReSharper disable InconsistentNaming — Harmony __result 约定

namespace ShunMod.Tweaks.Patches.Enchantments;

/// <summary>
///     基于白名单的附魔限制解除器。
///     从 <c>enchant_whitelist.json</c> 读取白名单，只对白名单内的附魔类型
///     解除 CanEnchant(CardModel) / CanEnchantCardType(CardType) 限制，
///     让任何卡牌都能接受被选中的附魔。
///     在 Harmony.PatchAll() 之后调用。
/// </summary>
internal static class EnchantRestrictionRemover
{
    private const string ModId = "ShunMod_Tweaks";
    private const string ConfigFileName = "enchant_whitelist.json";

    /// <summary>扫描所有已加载程序集，对白名单内的附魔类型 Patch 限制解除。</summary>
    public static void ApplyAll(Harmony harmony)
    {
        var whitelist = LoadWhitelist();
        if (whitelist.Count == 0)
        {
            Log.Info($"[{ModId}] EnchantRestrictionRemover: 白名单为空，跳过所有附魔限制解除");
            return;
        }

        Log.Info($"[{ModId}] EnchantRestrictionRemover: 白名单 ({whitelist.Count} 项)：{string.Join(", ", whitelist)}");

        var enchantmentType = typeof(EnchantmentModel);
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        var canEnchant = enchantmentType.GetMethod(
            nameof(EnchantmentModel.CanEnchant),
            flags,
            null,
            [typeof(CardModel)],
            null);

        var canEnchantCardType = enchantmentType.GetMethod(
            nameof(EnchantmentModel.CanEnchantCardType),
            flags,
            null,
            [typeof(CardType)],
            null);

        if (canEnchant == null && canEnchantCardType == null)
        {
            Log.Warn($"[{ModId}] EnchantRestrictionRemover: 找不到基类方法，跳过");
            return;
        }

        var patched = new List<string>();

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.IsDynamic)
                continue;

            try
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (!enchantmentType.IsAssignableFrom(type) || type == enchantmentType || type.IsAbstract)
                        continue;

                    // 只处理白名单内的类型
                    if (!whitelist.Contains(type.Name))
                        continue;

                    // ── CanEnchant(CardModel) override ──
                    if (canEnchant != null)
                    {
                        var method = type.GetMethod(
                            nameof(EnchantmentModel.CanEnchant),
                            flags,
                            null,
                            [typeof(CardModel)],
                            null);

                        if (method != null && method.DeclaringType == type && method.IsVirtual && method != canEnchant)
                        {
                            var postfix = new HarmonyMethod(typeof(EnchantRestrictionRemover),
                                nameof(ForceTruePostfix));
                            harmony.Patch(method, postfix: postfix);
                            patched.Add($"{type.Name}.CanEnchant");
                        }
                    }

                    // ── CanEnchantCardType(CardType) override ──
                    if (canEnchantCardType == null)
                        continue;

                    var ctMethod = type.GetMethod(
                        nameof(EnchantmentModel.CanEnchantCardType),
                        flags,
                        null,
                        [typeof(CardType)],
                        null);

                    if (ctMethod == null || ctMethod.DeclaringType != type || !ctMethod.IsVirtual ||
                        ctMethod == canEnchantCardType)
                        continue;

                    var prefix = new HarmonyMethod(typeof(EnchantRestrictionRemover),
                        nameof(SkipAndReturnTrue));
                    harmony.Patch(ctMethod, prefix);
                    patched.Add($"{type.Name}.CanEnchantCardType");
                }
            }
            catch (ReflectionTypeLoadException)
            {
                // 跳过无法加载类型的程序集
            }
        }

        if (patched.Count > 0)
            Log.Info($"[{ModId}] EnchantRestrictionRemover: 已 Patch {patched.Count} 个方法：\n  {string.Join("\n  ", patched)}");
        else
            Log.Warn($"[{ModId}] EnchantRestrictionRemover: 白名单内有 {whitelist.Count} 项，但未找到匹配的附魔类型（可能类型名拼写错误或对应 DLL 未加载）");
    }

    /// <summary>
    ///     从 DLL 同目录读取白名单 JSON 文件。
    ///     文件不存在或格式错误时返回空列表（安全默认）。
    /// </summary>
    private static HashSet<string> LoadWhitelist()
    {
        try
        {
            var dllPath = Assembly.GetExecutingAssembly().Location;
            var configPath = Path.Combine(Path.GetDirectoryName(dllPath) ?? ".", ConfigFileName);

            if (!File.Exists(configPath))
            {
                Log.Info($"[{ModId}] EnchantRestrictionRemover: 未找到配置文件 {configPath}，使用空白名单（不解除任何限制）");
                return [];
            }

            var json = File.ReadAllText(configPath);
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("whitelist", out var whitelistElement))
            {
                Log.Warn($"[{ModId}] EnchantRestrictionRemover: 配置文件缺少 \"whitelist\" 字段，使用空白名单");
                return [];
            }

            var list = new HashSet<string>();
            foreach (var item in whitelistElement.EnumerateArray())
                list.Add(item.GetString() ?? "");

            list.RemoveWhere(string.IsNullOrEmpty);
            return list;
        }
        catch (Exception e)
        {
            Log.Warn($"[{ModId}] EnchantRestrictionRemover: 读取配置文件失败: {e.GetType().Name}: {e.Message}，使用空白名单");
            return [];
        }
    }

    /// <summary>
    ///     CanEnchant Postfix：强制返回 true（保留原方法执行的副作用，但覆盖返回值）。
    /// </summary>
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    [SuppressMessage("ReSharper", "RedundantAssignment")]
    private static void ForceTruePostfix(ref bool __result)
    {
        __result = true;
    }

    /// <summary>
    ///     CanEnchantCardType Prefix：跳过原方法，直接返回 true。
    /// </summary>
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    [SuppressMessage("ReSharper", "RedundantAssignment")]
    private static bool SkipAndReturnTrue(ref bool __result)
    {
        __result = true;
        return false;
    }
}