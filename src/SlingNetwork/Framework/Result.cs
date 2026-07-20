using System.Diagnostics.CodeAnalysis;

namespace DotNetCampus.SlingNetwork.Framework;

public static class Result
{
    public static Result<T> From<T>(T value) where T : class
    {
        return new Result<T>
        {
            IsSuccess = true,
            Value = value,
            ErrorMessage = null,
        };
    }

    public static Result<T> Failed<T>(string errorMessage) where T : class
    {
        return new Result<T>
        {
            IsSuccess = false,
            Value = null,
            ErrorMessage = errorMessage,
        };
    }

    public static ErrorResult Failed(string errorMessage)
    {
        return new ErrorResult
        {
            ErrorMessage = errorMessage,
        };
    }

    public class ErrorResult
    {
        public required string ErrorMessage { get; init; }
    }
}

public readonly record struct Result<T> where T : class
{
    [MemberNotNullWhen(true, nameof(Value))]
    [MemberNotNullWhen(false, nameof(ErrorMessage))]
    public required bool IsSuccess { get; init; }

    public required T? Value { get; init; }

    public required string? ErrorMessage { get; init; }

    public static Result<T> Success(T value) => new()
    {
        IsSuccess = true,
        Value = value,
        ErrorMessage = null,
    };

    public static Result<T> Failed(string errorMessage) => new()
    {
        IsSuccess = false,
        Value = null,
        ErrorMessage = errorMessage,
    };

    public static implicit operator Result<T>(Result.ErrorResult errorResult)
    {
        return new Result<T>
        {
            IsSuccess = false,
            Value = null,
            ErrorMessage = errorResult.ErrorMessage,
        };
    }
}
