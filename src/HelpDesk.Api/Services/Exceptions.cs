namespace HelpDesk.Api.Services;

/// <summary>Thrown when a requested record does not exist (returned to the browser as HTTP 404).</summary>
public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}

/// <summary>Thrown when a request breaks a business rule (returned to the browser as HTTP 400).</summary>
public class BusinessRuleException : Exception
{
    public BusinessRuleException(string message) : base(message) { }
}
