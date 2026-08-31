namespace ChzzkApi_CS.Session;

/// <summary>
/// A failure raised inside a socket receive callback, which has no caller to throw to.
/// </summary>
/// <param name="TimeUtc">When the failure was recorded.</param>
/// <param name="Source">Socket event it came from: SYSTEM, CHAT, DONATION, SUBSCRIPTION, or SOCKET for transport failures.</param>
/// <param name="Exception">
/// The original exception, not wrapped. A <see cref="ChzzkApiException"/> means Chzzk itself
/// answered with an error code; anything else originated locally or in the transport.
/// </param>
public readonly record struct SessionError(DateTimeOffset TimeUtc, string Source, Exception Exception);
