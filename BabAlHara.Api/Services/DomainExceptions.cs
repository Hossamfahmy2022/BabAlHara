namespace BabAlHara.Api.Services;

public class DomainValidationException : Exception
{
    public IDictionary<string, string[]> Errors { get; }

    public DomainValidationException(string field, string message)
        : base(message)
    {
        Errors = new Dictionary<string, string[]>
        {
            [field] = [message]
        };
    }

    public DomainValidationException(IDictionary<string, string[]> errors)
        : base("يوجد أخطاء في البيانات المدخلة.")
    {
        Errors = errors;
    }
}

public class DomainConflictException(string message) : Exception(message);
