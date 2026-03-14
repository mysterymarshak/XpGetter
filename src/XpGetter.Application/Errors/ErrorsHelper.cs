using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using XpGetter.Application.Dto;
using XpGetter.Application.Extensions;

namespace XpGetter.Application.Errors;

public class AssertionException : Exception
{
    public AssertionException(string message) : base(message)
    {
    }
}

public static class ErrorsHelper
{
    public static void ThrowIfNull<TValue>(
        [NotNull] TValue? value,
        string formatString,
        object? formatValue1 = null,
        [CallerArgumentExpression(nameof(value))] string? conditionExpression = null)
    {
        if (value is null)
        {
            var message = formatValue1 is null ? formatString : string.Format(formatString, formatValue1);
            ThrowInternal(message, $"{conditionExpression} is not null");
        }
    }

    public static bool FailIfNull<TError, TValue>(
        [NotNullWhen(false)] TValue? value,
        string message,
        SteamSession session,
        [NotNullWhen(true)] out TError? error,
        [CallerArgumentExpression(nameof(value))] string? conditionExpression = null) where TError : BaseError, new()
        => FailIfNull(value, message.BindSession(session), out error, conditionExpression);

    public static bool FailIfNull<TError, TValue>(
        [NotNullWhen(false)] TValue? value,
        string message,
        [NotNullWhen(true)] out TError? error,
        [CallerArgumentExpression(nameof(value))] string? conditionExpression = null) where TError : BaseError, new()
        => InternalCheck(value is null, message, $"{conditionExpression} is not null", out error);

    public static bool FailIf<T>(
        bool condition,
        string message,
        SteamSession session,
        [NotNullWhen(true)] out T? error,
        [CallerArgumentExpression(nameof(condition))] string? conditionExpression = null) where T : BaseError, new()
        => FailIf(condition, message.BindSession(session), out error, conditionExpression);

    public static bool FailIf<T>(
        bool condition,
        string message,
        [NotNullWhen(true)] out T? error,
        [CallerArgumentExpression(nameof(condition))]
        string? conditionExpression = null) where T : BaseError, new()
        => InternalCheck(condition, message, conditionExpression, out error,
            $"Condition is true ({conditionExpression}): {message}");

    public static bool Assert<T>(
        bool condition,
        string message,
        SteamSession session,
        [NotNullWhen(false)] out T? error,
        [CallerArgumentExpression(nameof(condition))] string? conditionExpression = null) where T : BaseError, new()
        => Assert(condition, message.BindSession(session), out error, conditionExpression);

    public static bool Assert<T>(
        bool condition,
        string message,
        [NotNullWhen(false)] out T? error,
        [CallerArgumentExpression(nameof(condition))] string? conditionExpression = null) where T : BaseError, new()
        => !InternalCheck(!condition, message, conditionExpression, out error);

    private static bool InternalCheck<T>(
        bool shouldFail,
        string message,
        string? expression,
        out T? error,
        string? customMessage = null) where T : BaseError, new()
    {
        error = shouldFail ? new T
        {
            Message = customMessage ?? $"Assertion failed ({expression}): {message}"
        } : null;
        return shouldFail;
    }

    [DoesNotReturn]
    private static void ThrowInternal(string message, string? expression)
    {
        throw new AssertionException($"Assertion failed ({expression}): {message}");
    }
}
