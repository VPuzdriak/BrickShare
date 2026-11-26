using System;

namespace BrickShare.Catalog.Domain;

public sealed class SetSpecificationUnavailableException : Exception
{
    public SetSpecificationUnavailableException()
    {
    }

    public SetSpecificationUnavailableException(string message)
        : base(message)
    {
    }

    public SetSpecificationUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}