using System;

namespace TexasOperationalPortal.Application.Common.Exceptions;

public sealed class DownstreamException : Exception
{
    public int StatusCode { get; }

    public DownstreamException(string message) : base(message)
    {
        StatusCode = 502; // Bad Gateway
    }

    public DownstreamException(int statusCode, string message) : base(message)
    {
        StatusCode = statusCode;
    }

    public DownstreamException(int statusCode, string message, Exception? innerException) 
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }
}
