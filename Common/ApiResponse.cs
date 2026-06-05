namespace PortfolioApi.Common;

/// <summary>
/// Standardised JSON envelope for all API responses.
/// Ensures the frontend always gets a consistent shape.
/// </summary>
public class ApiResponse<T>
{
    public bool          Success  { get; set; }
    public string        Message  { get; set; } = string.Empty;
    public T?            Data     { get; set; }
    public List<string>? Errors   { get; set; }
    public int           StatusCode { get; set; }

    public static ApiResponse<T> Ok(T data, string message = "Success") =>
        new() { Success = true, Message = message, Data = data, StatusCode = 200 };

    public static ApiResponse<T> Created(T data, string message = "Created") =>
        new() { Success = true, Message = message, Data = data, StatusCode = 201 };

    public static ApiResponse<T> Fail(string message, int statusCode = 400, List<string>? errors = null) =>
        new() { Success = false, Message = message, StatusCode = statusCode, Errors = errors };
}

/// <summary>Non-generic version for responses that carry no data payload.</summary>
public class ApiResponse : ApiResponse<object>
{
    public static ApiResponse OkNoData(string message = "Success") =>
        new() { Success = true, Message = message, StatusCode = 200 };

    public static new ApiResponse Fail(string message, int statusCode = 400, List<string>? errors = null) =>
        new() { Success = false, Message = message, StatusCode = statusCode, Errors = errors };
}
