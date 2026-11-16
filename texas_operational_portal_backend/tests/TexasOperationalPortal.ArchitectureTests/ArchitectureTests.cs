using NetArchTest.Rules;
using System;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using Xunit;

namespace TexasOperationalPortal.ArchitectureTests;

public class ArchitectureTests
{
    private static readonly Assembly DomainAssembly = typeof(Domain.Entities.OperationalUser).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(Application.DependencyInjection).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(Infrastructure.Persistence.OperationalDbContext).Assembly;
    private static readonly Assembly ApiAssembly = typeof(Program).Assembly;

    [Fact]
    public void Domain_ShouldNot_HaveDependencyOnOtherProjects()
    {
        var otherProjects = new[]
        {
            "TexasOperationalPortal.Application",
            "TexasOperationalPortal.Infrastructure",
            "TexasOperationalPortal.Api"
        };

        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(otherProjects)
            .GetResult();

        Assert.True(result.IsSuccessful, "Domain assembly must not have dependencies on other projects.");
    }

    [Fact]
    public void Application_ShouldNot_HaveDependencyOnInfrastructureOrApi()
    {
        var otherProjects = new[]
        {
            "TexasOperationalPortal.Infrastructure",
            "TexasOperationalPortal.Api"
        };

        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(otherProjects)
            .GetResult();

        Assert.True(result.IsSuccessful, "Application assembly must not have dependencies on Infrastructure or Api projects.");
    }

    [Fact]
    public void Infrastructure_ShouldNot_HaveDependencyOnApi()
    {
        var otherProjects = new[]
        {
            "TexasOperationalPortal.Api"
        };

        var result = Types.InAssembly(InfrastructureAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(otherProjects)
            .GetResult();

        Assert.True(result.IsSuccessful, "Infrastructure assembly must not have dependencies on Api project.");
    }

    [Fact]
    public void ApiControllers_ShouldNot_Reference_DbContextDirectly()
    {
        var result = Types.InAssembly(ApiAssembly)
            .That()
            .Inherit(typeof(Microsoft.AspNetCore.Mvc.ControllerBase))
            .ShouldNot()
            .HaveDependencyOn("TexasOperationalPortal.Infrastructure.Persistence.OperationalDbContext")
            .GetResult();

        Assert.True(result.IsSuccessful, "Api controllers must not reference the DbContext directly.");
    }

    [Fact]
    public void Domain_ShouldNot_ContainBookingEntities()
    {
        var failingTypes = DomainAssembly.GetTypes()
            .Where(t => t.Name.StartsWith("Visa", StringComparison.OrdinalIgnoreCase) ||
                        t.Name.StartsWith("Vfs", StringComparison.OrdinalIgnoreCase) ||
                        t.Name.StartsWith("Holiday", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.Empty(failingTypes);
    }

    [Fact]
    public void OperationalPortal_ShouldNot_DependOnSalesPortal()
    {
        var assemblies = new[] { DomainAssembly, ApplicationAssembly, InfrastructureAssembly, ApiAssembly };
        var otherProjects = new[] { "TexasPortal" };

        foreach (var assembly in assemblies)
        {
            var result = Types.InAssembly(assembly)
                .ShouldNot()
                .HaveDependencyOnAny(otherProjects)
                .GetResult();

            Assert.True(result.IsSuccessful, $"Assembly {assembly.GetName().Name} must not reference Sales Portal assembly.");
        }
    }
}
