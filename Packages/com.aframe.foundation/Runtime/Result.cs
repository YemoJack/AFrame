namespace AFrame.Foundation
{
    public readonly struct Result
    {
        private Result(bool succeeded, AFrameError error)
        {
            Succeeded = succeeded;
            Error = error;
        }

        public bool Succeeded { get; }
        public bool Failed => !Succeeded;
        public AFrameError Error { get; }

        public static Result Ok()
        {
            return new Result(true, default);
        }

        public static Result Fail(string code, string message)
        {
            return new Result(false, new AFrameError(code, message));
        }
    }

    public readonly struct Result<T>
    {
        private Result(bool succeeded, T value, AFrameError error)
        {
            Succeeded = succeeded;
            Value = value;
            Error = error;
        }

        public bool Succeeded { get; }
        public bool Failed => !Succeeded;
        public T Value { get; }
        public AFrameError Error { get; }

        public static Result<T> Ok(T value)
        {
            return new Result<T>(true, value, default);
        }

        public static Result<T> Fail(string code, string message)
        {
            return new Result<T>(false, default, new AFrameError(code, message));
        }
    }
}
