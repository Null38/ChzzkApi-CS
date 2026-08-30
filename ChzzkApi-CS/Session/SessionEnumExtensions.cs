namespace ChzzkApi_CS.Session;

public static class SessionEnumExtensions
{
    public static string ToPathSegment(this EventType eventType) => eventType switch
    {
        EventType.Chat => "chat",
        EventType.Donation => "donation",
        EventType.Subscription => "subscription",
        _ => throw new ArgumentOutOfRangeException(nameof(eventType), eventType, null)
    };
}