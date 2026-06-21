using System;

namespace TradingService.Application.Common.Models
{
    /// <summary>
    /// Represents the outcome of an operation. Success carries a value; failure carries an <see cref="Error"/>.
    /// </summary>
    public sealed class Result<T>
    {
        public bool IsSuccess { get; }
        public bool IsFailure => !IsSuccess;
        public T? Value { get; }
        public Error Error { get; }

        private Result(T value)
        {
            IsSuccess = true;
            Value = value;
            Error = Error.None;
        }

        private Result(Error error)
        {
            IsSuccess = false;
            Value = default;
            Error = error;
        }

        public static Result<T> Success(T value) => new(value);
        public static Result<T> Failure(Error error) => new(error);

        public static implicit operator Result<T>(T value) => Success(value);
        public static implicit operator Result<T>(Error error) => Failure(error);
    }

    /// <summary>
    /// Non-generic result used for commands that do not return data.
    /// </summary>
    public sealed class Result
    {
        public bool IsSuccess { get; }
        public bool IsFailure => !IsSuccess;
        public Error Error { get; }

        private Result(bool isSuccess, Error error)
        {
            IsSuccess = isSuccess;
            Error = error;
        }

        public static Result Success() => new(true, Error.None);
        public static Result Failure(Error error) => new(false, error);
        
        public static Result<TValue> Success<TValue>(TValue value) => Result<TValue>.Success(value);
        public static Result<TValue> Failure<TValue>(Error error) => Result<TValue>.Failure(error);

        public static implicit operator Result(Error error) => Failure(error);
    }
}
