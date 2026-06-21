namespace TradingService.Application.Common.Models
{
    public enum ErrorType
    {
        None,
        NotFound,
        Validation,
        Conflict,
        Unauthorized,
        Forbidden,
        Failure
    }

    public sealed class Error
    {
        public static readonly Error None = new(string.Empty, string.Empty, ErrorType.None);
        public string Code { get; }
        public string Description { get; }
        public ErrorType Type { get; }

        private Error(string code, string description, ErrorType type)
        {
            Code = code;
            Description = description;
            Type = type;
        }

        public static Error NotFound(string code, string description) => new(code, description, ErrorType.NotFound);
        public static Error Validation(string code, string description) => new(code, description, ErrorType.Validation);
        public static Error Conflict(string code, string description) => new(code, description, ErrorType.Conflict);
        public static Error Unauthorized(string code, string description) => new(code, description, ErrorType.Unauthorized);
        public static Error Forbidden(string code, string description) => new(code, description, ErrorType.Forbidden);
        public static Error Failure(string code, string description) => new(code, description, ErrorType.Failure);

        public override string ToString() => $"{Code}: {Description}";
    }
}
