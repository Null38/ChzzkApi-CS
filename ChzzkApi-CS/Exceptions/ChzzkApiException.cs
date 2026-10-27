using ChzzkApi;
using System.Net;

namespace ChzzkApi_CS.Exceptions;

public enum ChzzkAuthError
{
    NotUnauthorized = -1,
    Unauthorized,
    InvalidClient,
    InvalidToken
}

public class ChzzkApiException : Exception
{
    private const string InvalidClientMessage = "INVALID_CLIENT";
    private const string InvalidTokenMessage = "INVALID_TOKEN";

    public ChzzkResponse Response { get; }
    public HttpStatusCode StatusCode => Response.Code;

    public ChzzkApiException(ChzzkResponse response)
        : base($"[{(int)response.Code}] {response.Code} : {response.Message ?? "Unknown error"}")
    {
        Response = response;
    }

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

public class ChzzkResponseException : Exception
{
    public HttpStatusCode StatusCode { get; }
    public string? Body { get; }

    public ChzzkResponseException(HttpStatusCode statusCode, string? body, Exception? innerException = null)
        : base($"[{(int)statusCode}] {statusCode} : Response is not a valid Chzzk envelope", innerException)
    {
        StatusCode = statusCode;
        Body = body;
    }
}

public class ChzzkAuthorizationException : Exception
{
    public string? Error { get; }
    public string? ErrorDescription { get; }

    public ChzzkAuthorizationException(string? error, string? errorDescription)
        : base(error is null
            ? "Authorization callback did not include a code"
            : $"Authorization failed: {error} : {errorDescription ?? "Unknown error"}")
    {
        Error = error;
        ErrorDescription = errorDescription;
    }
}
