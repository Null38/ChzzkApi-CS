using ChzzkApi.Models;
using System.Collections.Specialized;
using System.Net;

namespace ChzzkApi.Exceptions;

public enum ChzzkAuthError
{
    NotUnauthorized = -1,
    Unauthorized,
    InvalidClient,
    InvalidToken
}

public class ChzzkApiException(ChzzkResponse response)
    : Exception($"[{(int)response.Code}] {response.Code} : {response.Message ?? "Unknown error"}")
{
    private const string InvalidClientMessage = "INVALID_CLIENT";
    private const string InvalidTokenMessage = "INVALID_TOKEN";

    public ChzzkResponse Response { get; } = response;
    public HttpStatusCode StatusCode => Response.Code;

    public ChzzkAuthError GetAuthError() => GetAuthError(Response);

    public static ChzzkAuthError GetAuthError(ChzzkResponse response)
    {
        if (response.Code != HttpStatusCode.Unauthorized)
            return ChzzkAuthError.NotUnauthorized;

        return response.Message switch
        {
            InvalidClientMessage => ChzzkAuthError.InvalidClient,
            InvalidTokenMessage => ChzzkAuthError.InvalidToken,
            _ => ChzzkAuthError.Unauthorized
        };
    }
}

public class ChzzkResponseException(HttpStatusCode statusCode, string? body, Exception? innerException = null) 
    : Exception($"[{(int)statusCode}] {statusCode} : Response is not a valid Chzzk envelope", innerException)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
    public string? Body { get; } = body;
}

public class ChzzkAuthorizationException(NameValueCollection query)
    : Exception("Authorization callback did not include a code")
{
    public NameValueCollection Query { get; } = new(query);
}
