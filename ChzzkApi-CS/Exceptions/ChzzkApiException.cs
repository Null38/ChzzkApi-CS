using ChzzkApi.Models;
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

public class ChzzkAuthorizationException(string? error, string? errorDescription) 
    : Exception(error is null ? "Authorization callback did not include a code" : $"Authorization failed: {error} : {errorDescription ?? "Unknown error"}")
{
    public string? Error { get; } = error;
    public string? ErrorDescription { get; } = errorDescription;
}
