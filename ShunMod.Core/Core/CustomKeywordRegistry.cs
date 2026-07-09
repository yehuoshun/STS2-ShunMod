using System.Collections.Generic;

namespace ShunMod.Core;

/// <summary>
///     自定义词条注册表 — 跟踪哪些卡牌拥有关键词。
///     因为 CardKeyword 是原版编译枚举无法扩展，用字符串 ID 体系替代。
/// </summary>
public static class CustomKeywordRegistry
{
    private static readonly Dictionary<string, HashSet<string>> CardKeywords = new();
    private static readonly Dictionary<string, KeywordDefinition> KeywordDefinitions = new();

    /// <summary>注册一个词条定义。</summary>
    public static void DefineKeyword(string id, string title, string description,
        KeywordDisplayPosition position = KeywordDisplayPosition.AfterDescription)
    {
        KeywordDefinitions[id] = new KeywordDefinition(id, title, description, position);
    }

    /// <summary>为指定卡牌类型注册词条。</summary>
    public static void RegisterKeyword(Type cardType, string keywordId)
    {
        var key = cardType.FullName!;
        if (!CardKeywords.ContainsKey(key))
            CardKeywords[key] = new HashSet<string>();
        CardKeywords[key].Add(keywordId);
    }

    /// <summary>检查卡牌实例是否拥有指定词条。</summary>
    public static bool HasKeyword(object? card, string keywordId)
    {
        if (card == null) return false;
        var key = card.GetType().FullName;
        return key != null && CardKeywords.TryGetValue(key, out var set) && set.Contains(keywordId);
    }

    /// <summary>获取卡牌实例的所有自定义词条 ID。</summary>
    public static IEnumerable<string> GetKeywords(object? card)
    {
        if (card == null) yield break;
        var key = card.GetType().FullName;
        if (key != null && CardKeywords.TryGetValue(key, out var set))
            foreach (var kw in set)
                yield return kw;
    }

    /// <summary>获取词条定义。</summary>
    public static KeywordDefinition? GetDefinition(string keywordId)
    {
        return KeywordDefinitions.TryGetValue(keywordId, out var def) ? def : null;
    }

    /// <summary>获取所有已注册的词条定义。</summary>
    public static IEnumerable<KeywordDefinition> AllDefinitions => KeywordDefinitions.Values;
}

public class KeywordDefinition
{
    public string Id { get; }
    public string Title { get; }
    public string Description { get; }
    public KeywordDisplayPosition Position { get; }

    public KeywordDefinition(string id, string title, string description,
        KeywordDisplayPosition position = KeywordDisplayPosition.AfterDescription)
    {
        Id = id;
        Title = title;
        Description = description;
        Position = position;
    }
}

public enum KeywordDisplayPosition
{
    BeforeDescription,
    AfterDescription
}