using System;

namespace RFiDGear.ExtensionLibraries.CardCheck
{
    /// <summary>
    /// The outcome of an operation that returns nothing: it worked, or it failed with a <see cref="Failure"/>.
    /// Services return this instead of swallowing errors or throwing them across the library boundary.
    /// </summary>
    public sealed class Result
    {
        private static readonly Result Succeeded = new Result(null);

        private Result(Failure? failure)
        {
            Failure = failure;
        }

        /// <summary>Whether the operation worked.</summary>
        public bool IsSuccess => Failure == null;

        /// <summary>Why it did not work, or <c>null</c> when it did.</summary>
        public Failure? Failure { get; }

        /// <summary>Creates the successful result.</summary>
        /// <returns>A result that is a success.</returns>
        public static Result Ok() => Succeeded;

        /// <summary>Creates a failed result.</summary>
        /// <param name="failure">Why the operation failed.</param>
        /// <returns>A result that is a failure.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="failure"/> is null.</exception>
        public static Result Fail(Failure failure) =>
            new Result(failure ?? throw new ArgumentNullException(nameof(failure)));
    }

    /// <summary>
    /// The outcome of an operation that returns a value: the value, or a <see cref="Failure"/>.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    public sealed class Result<T>
    {
        private readonly T? _value;

        private Result(T? value, Failure? failure)
        {
            _value = value;
            Failure = failure;
        }

        /// <summary>Whether the operation worked.</summary>
        public bool IsSuccess => Failure == null;

        /// <summary>Why it did not work, or <c>null</c> when it did.</summary>
        public Failure? Failure { get; }

        /// <summary>The value of a successful result.</summary>
        /// <exception cref="InvalidOperationException">The result is a failure; check <see cref="IsSuccess"/> first.</exception>
        public T Value => IsSuccess
            ? _value!
            : throw new InvalidOperationException("A failed result has no value: " + Failure!.Code);

        /// <summary>Creates a successful result.</summary>
        /// <param name="value">The value.</param>
        /// <returns>A result that is a success and holds <paramref name="value"/>.</returns>
        public static Result<T> Ok(T value) => new Result<T>(value, null);

        /// <summary>Creates a failed result.</summary>
        /// <param name="failure">Why the operation failed.</param>
        /// <returns>A result that is a failure.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="failure"/> is null.</exception>
        public static Result<T> Fail(Failure failure) =>
            new Result<T>(default, failure ?? throw new ArgumentNullException(nameof(failure)));
    }
}
