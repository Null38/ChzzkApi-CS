using System.Diagnostics.CodeAnalysis;

namespace ChzzkApi;

public class RuleEntry
{
    public string? Key { get; }
    public ChzzkRule Rule { get; set; }

    internal RuleEntry(string? key, ChzzkRule rule)
    {
        Key = key; 
        Rule = rule;
    }
}

public class RuleList
{
    private readonly List<RuleEntry> entries = [];
    private readonly Dictionary<string, RuleEntry> keyed = [];

    public IReadOnlyList<RuleEntry> Entries => entries;

    internal RuleList() { }

    public void Add(string? key, ChzzkRule rule)
    {
        if (key is not null && keyed.TryGetValue(key, out var existing))
        {
            existing.Rule = rule;
            return;
        }

        var entry = new RuleEntry(key, rule);
        entries.Add(entry);

        if (key is not null)
            keyed[key] = entry;
    }

    public void Insert(int index, string? key, ChzzkRule rule)
    {
        var entry = new RuleEntry(key, rule);
        entries.Insert(index, entry);

        if (key is null)
            return;

        if (keyed.TryGetValue(key, out var existing))
            entries.Remove(existing);

        keyed[key] = entry;
    }

    public bool Remove(string key)
    {
        if (!keyed.Remove(key, out var entry))
            return false;

        entries.Remove(entry);
        return true;
    }

    public void RemoveAt(int index)
    {
        var entry = entries[index];
        entries.RemoveAt(index);

        if (entry.Key is not null)
            keyed.Remove(entry.Key);
    }

    public bool TryGet(string key, [NotNullWhen(true)] out RuleEntry? entry) 
        => keyed.TryGetValue(key, out entry);

    public bool Contains(string key) 
        => keyed.ContainsKey(key);
}