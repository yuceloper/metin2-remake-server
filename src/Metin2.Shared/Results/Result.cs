namespace Metin2.Shared.Results;

public readonly record struct Result
{
    private Result(bool isSuccess, Error error)
    {
        if (isSuccess && !error.IsNone)
        {
            throw new ArgumentException("A successful result cannot contain an error.", nameof(error));
        }

        if (!isSuccess && error.IsNone)
        {
            throw new ArgumentException("A failed result must contain an error.", nameof(error));
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public Error Error { get; }

    public static Result Success() => new(true, Error.None);

    public static Result Failure(Error error) => new(false, error);
}

public readonly record struct Result<T>
{
    private readonly T? _value;

    private Result(T value)
    {
        IsSuccess = true;
        Error = Error.None;
        _value = value;
    }

    private Result(Error error)
    {
        if (error.IsNone)
        {
            throw new ArgumentException("A failed result must contain an error.", nameof(error));
        }

        IsSuccess = false;
        Error = error;
        _value = default;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public Error Error { get; }

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("A failed result does not contain a value.");

    public static Result<T> Success(T value) => new(value);

    public static Result<T> Failure(Error error) => new(error);
}
