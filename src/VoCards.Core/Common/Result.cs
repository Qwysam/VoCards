namespace VoCards.Core.Common;

/// <summary>
/// The outcome of an operation that is allowed to fail for ordinary reasons.
///
/// The 2021 app handled bad input by spinning in <c>for(;;)</c> until the user
/// typed something acceptable, and handled impossible input by throwing. Both are
/// replaced by returning one of these and letting the caller decide.
/// </summary>
public readonly record struct Result
{
    private Result(bool ok, string? error)
    {
        IsSuccess = ok;
        Error = error;
    }

    public bool IsSuccess { get; }

    public string? Error { get; }

    public bool IsFailure => !IsSuccess;

    public static Result Success() => new(true, null);

    public static Result Failure(string error) => new(false, error);

    public static Result<T> Success<T>(T value) => Result<T>.Success(value);

    public static Result<T> Failure<T>(string error) => Result<T>.Failure(error);
}

/// <summary>A <see cref="Result"/> that carries a value when it succeeds.</summary>
public readonly record struct Result<T>
{
    private Result(bool ok, T? value, string? error)
    {
        IsSuccess = ok;
        Value = value;
        Error = error;
    }

    public bool IsSuccess { get; }

    public T? Value { get; }

    public string? Error { get; }

    public bool IsFailure => !IsSuccess;

    public static Result<T> Success(T value) => new(true, value, null);

    public static Result<T> Failure(string error) => new(false, default, error);

    /// <summary>Returns the value, or <paramref name="fallback"/> when this is a failure.</summary>
    public T OrElse(T fallback) => IsSuccess && Value is not null ? Value : fallback;

    /// <summary>Transforms the value, propagating a failure untouched.</summary>
    public Result<TOut> Map<TOut>(Func<T, TOut> selector) =>
        IsSuccess && Value is not null
            ? Result<TOut>.Success(selector(Value))
            : Result<TOut>.Failure(Error ?? "Unknown error.");

    public static implicit operator Result<T>(T value) => Success(value);
}
