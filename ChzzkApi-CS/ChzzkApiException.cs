namespace ChzzkApi_CS;

public class ChzzkApiException : Exception
{
    public static class StatusCode
    {
        public const int Success = 200;
        public const int BadRequest = 400;
        public const int Unauthorized = 401;
        public const int Forbidden = 403;
        public const int NotFound = 404;
        public const int TooManyRequests = 429;
        public const int InternalServerError = 500;
    }

    public int Code { get; }

    public ChzzkApiException(int code, string? message) : base($"[{code}] {message ?? "Unknown error"}")
    {
        Code = code;
    }

    public ChzzkApiException(ChzzkResponse response) : base($"[{response.Code}] {response.Message ?? "Unknown error"}")
    {
        Code = response.Code;
    }
}