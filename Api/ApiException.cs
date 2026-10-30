using System;

namespace WarmachineClient.Api;

public class ApiException : Exception
{
    public int Status { get; }
    public string? Title { get; }
    public string? Detail { get; }

    public ApiException(int status, string? title, string? detail)
        : base(detail ?? title ?? $"API request failed with status {status}.")
    {
        Status = status;
        Title = title;
        Detail = detail;
    }
}
