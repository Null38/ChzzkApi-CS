namespace ChzzkApi.Models;

public static class ChzzkEnumExtensions
{
    public static string ToPathSegment(this EventType eventType) => eventType switch
    {
        EventType.Chat => "chat",
        EventType.Donation => "donation",
        EventType.Subscription => "subscription",
        _ => throw new ArgumentOutOfRangeException(nameof(eventType), eventType, null)
    };

    public static string ToParamSegment(this SortOption option) => option switch
    {
        SortOption.Recent => "RECENT",
        SortOption.Longer => "LONGER",
        _ => throw new ArgumentOutOfRangeException(nameof(option), option, null)
    };

    public static string ToParamSegment(this ChatAvailableConditionOption option) => option switch
    {
        ChatAvailableConditionOption.None => "NONE",
        ChatAvailableConditionOption.RealName => "REAL_NAME",
        _ => throw new ArgumentOutOfRangeException(nameof(option), option, null)
    };

    public static string ToParamSegment(this ChatAvailableGroupOption option) => option switch
    {
        ChatAvailableGroupOption.All => "ALL",
        ChatAvailableGroupOption.Follower => "FOLLOWER",
        ChatAvailableGroupOption.Manager => "MANAGER",
        ChatAvailableGroupOption.Subscriber => "SUBSCRIBER",
        _ => throw new ArgumentOutOfRangeException(nameof(option), option, null)
    };

    public static string ToParamSegment(this CategoryType type) => type switch
    {
        CategoryType.Game => "GAME",
        CategoryType.Sports => "SPORTS",
        CategoryType.Etc => "ETC",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
    };
}