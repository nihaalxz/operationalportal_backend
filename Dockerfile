FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["texas_operational_portal_backend/src/TexasOperationalPortal.Api/TexasOperationalPortal.Api.csproj", "texas_operational_portal_backend/src/TexasOperationalPortal.Api/"]
COPY ["texas_operational_portal_backend/src/TexasOperationalPortal.Application/TexasOperationalPortal.Application.csproj", "texas_operational_portal_backend/src/TexasOperationalPortal.Application/"]
COPY ["texas_operational_portal_backend/src/TexasOperationalPortal.Infrastructure/TexasOperationalPortal.Infrastructure.csproj", "texas_operational_portal_backend/src/TexasOperationalPortal.Infrastructure/"]
COPY ["texas_operational_portal_backend/src/TexasOperationalPortal.Domain/TexasOperationalPortal.Domain.csproj", "texas_operational_portal_backend/src/TexasOperationalPortal.Domain/"]
COPY ["texas_operational_portal_backend/src/TexasOperationalPortal.Contracts/TexasOperationalPortal.Contracts.csproj", "texas_operational_portal_backend/src/TexasOperationalPortal.Contracts/"]

RUN dotnet restore "texas_operational_portal_backend/src/TexasOperationalPortal.Api/TexasOperationalPortal.Api.csproj"

COPY . .

WORKDIR "/src/texas_operational_portal_backend/src/TexasOperationalPortal.Api"
RUN dotnet publish "TexasOperationalPortal.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "TexasOperationalPortal.Api.dll"]
