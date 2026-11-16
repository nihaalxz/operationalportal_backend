using Microsoft.Extensions.DependencyInjection;
using TexasOperationalPortal.Application.Features.Auth;
using TexasOperationalPortal.Application.Features.Dashboard;
using TexasOperationalPortal.Application.Features.Visas;
using TexasOperationalPortal.Application.Features.Vfs;
using TexasOperationalPortal.Application.Features.Holidays;
using TexasOperationalPortal.Application.Features.Staff;

namespace TexasOperationalPortal.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<LoginUseCase>();
        services.AddScoped<RefreshTokenUseCase>();
        services.AddScoped<StaffSyncUseCase>();
        services.AddScoped<GetOperationalDashboardUseCase>();
        services.AddScoped<GetVisaQueueUseCase>();
        services.AddScoped<GetVisaDetailsUseCase>();
        services.AddScoped<GetVfsQueueUseCase>();
        services.AddScoped<GetVfsDetailsUseCase>();
        services.AddScoped<GetHolidayQueueUseCase>();
        services.AddScoped<GetHolidayDetailsUseCase>();
        services.AddScoped<TexasOperationalPortal.Application.Features.Assignments.IAssignmentService, TexasOperationalPortal.Application.Features.Assignments.AssignmentService>();

        return services;
    }
}
