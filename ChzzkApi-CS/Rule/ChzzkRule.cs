using ChzzkApi;

namespace ChzzkApi_CS.Rule;

public delegate Task ChzzkRule(ChzzkRuleContext context, CancellationToken ct);

public class ChzzkRuleContext
{
    private readonly Func<CancellationToken, Task<ChzzkResponse>> resend;

    internal ChzzkRuleContext(ChzzkResponse response, ChzzkClient client, ChzzkUser? user, ChzzkUser.ChzzkToken? token, Func<CancellationToken, Task<ChzzkResponse>> resend)
    {
        Response = response;
        Client = client;
        User = user;
        SentToken = token;
        this.resend = resend;
    }

    public ChzzkResponse Response { get; private set; }
    public ChzzkClient Client { get; }
    public ChzzkUser? User { get; }
    public ChzzkUser.ChzzkToken? SentToken { get; }

    public async Task<ChzzkResponse> ResendAsync(CancellationToken ct = default)
    {
        Response = await resend(ct);
        return Response;
    }
}
