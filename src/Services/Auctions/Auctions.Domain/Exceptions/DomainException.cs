namespace Auctions.Domain.Exceptions;

/// <summary>Thrown when an operation would break a domain invariant.</summary>
public class DomainException : Exception
{
    public DomainException() { }

    public DomainException(string message) : base(message) { }

    public DomainException(string message, Exception innerException) : base(message, innerException) { }
}
