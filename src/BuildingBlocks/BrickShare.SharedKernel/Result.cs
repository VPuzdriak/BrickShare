using System;

namespace BrickShare.SharedKernel;

public sealed class Result
{
    private Result(DomainError? error)
    {
        Error = error;
    }

    public DomainError? Error { get; }

    public bool IsSuccess => Error is null;

    public bool IsFailure => !IsSuccess;

    public static Result Success() => new(null);

    public static Result<T> Success<T>(T value) => new(value, null);

    public static Result Failure(DomainError error) => new(error);

    public static Result<T> Failure<T>(DomainError error) => new(default, error);
}

public sealed class Result<T>
{
    private readonly T? _value;

    internal Result(T? value, DomainError? error)
    {
        _value = value;
        Error = error;
    }

    public DomainError? Error { get; }

    public bool IsSuccess => Error is null;

    public bool IsFailure => !IsSuccess;

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("A failed result has no value. Check IsSuccess first.");
}