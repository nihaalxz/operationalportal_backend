using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using System;
using TexasOperationalPortal.Application.Common.Exceptions;

namespace TexasOperationalPortal.Api.Filters;

public sealed class DownstreamExceptionFilter : IExceptionFilter
{
    private readonly ILogger<DownstreamExceptionFilter> _logger;

    public DownstreamExceptionFilter(ILogger<DownstreamExceptionFilter> logger)
    {
        _logger = logger;
    }

    public void OnException(ExceptionContext context)
    {
        if (context.Exception is DownstreamException downstreamEx)
        {
            _logger.LogError(downstreamEx, "Downstream API Exception occurred: {Message} (StatusCode: {StatusCode})", 
                downstreamEx.Message, downstreamEx.StatusCode);

            var mappedStatus = downstreamEx.StatusCode switch
            {
                401 => StatusCodes.Status502BadGateway,
                403 => StatusCodes.Status502BadGateway,
                404 => StatusCodes.Status404NotFound,
                429 => StatusCodes.Status503ServiceUnavailable,
                504 => StatusCodes.Status504GatewayTimeout,
                _   => StatusCodes.Status502BadGateway
            };

            var title = mappedStatus switch
            {
                StatusCodes.Status404NotFound => "Booking Not Found",
                StatusCodes.Status503ServiceUnavailable => "Downstream Service Unavailable",
                StatusCodes.Status504GatewayTimeout => "Downstream Service Timeout",
                _ => "Bad Downstream Gateway"
            };

            var detail = mappedStatus switch
            {
                StatusCodes.Status404NotFound => "The requested booking ID does not exist.",
                StatusCodes.Status503ServiceUnavailable => "The booking service is busy or offline. Please retry in a moment.",
                StatusCodes.Status504GatewayTimeout => "The downstream request timed out.",
                _ => "Received an invalid or unauthorized response from the booking gateway."
            };

            var problem = new ProblemDetails
            {
                Status = mappedStatus,
                Title = title,
                Detail = detail
            };

            context.Result = new ObjectResult(problem)
            {
                StatusCode = mappedStatus
            };

            context.ExceptionHandled = true;
        }
    }
}
