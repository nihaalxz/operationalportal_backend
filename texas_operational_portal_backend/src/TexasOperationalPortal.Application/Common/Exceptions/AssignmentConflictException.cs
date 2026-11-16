using System;

namespace TexasOperationalPortal.Application.Common.Exceptions;

public sealed class AssignmentConflictException : Exception
{
    public AssignmentConflictException(string message) : base(message)
    {
    }

    public AssignmentConflictException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
