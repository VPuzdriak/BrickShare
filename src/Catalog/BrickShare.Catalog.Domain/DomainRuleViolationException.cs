namespace BrickShare.Catalog.Domain;

public sealed class DomainRuleViolationException : Exception
{
    public DomainRuleViolationException()
    {
    }

    public DomainRuleViolationException(string message)
        : base(message)
    {
    }

    public DomainRuleViolationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
