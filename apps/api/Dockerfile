# syntax=docker/dockerfile:1

# SupportDesk API. Build from the repository root:
#   docker build -f apps/api/Dockerfile -t supportdesk-api .
# Nothing configurable is baked in: the connection string, JWT signing key, seed password and
# environment name all come from environment variables at run time (see docker-compose.yml).

# ---- build: .NET 10 SDK (satisfies global.json 10.0.100 + rollForward latestFeature) ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, from only the files restore reads, so this layer is reused until a project
# file, the shared props or global.json changes - not on every source edit.
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY apps/api/src/SupportDesk.Domain/SupportDesk.Domain.csproj apps/api/src/SupportDesk.Domain/
COPY apps/api/src/SupportDesk.Application/SupportDesk.Application.csproj apps/api/src/SupportDesk.Application/
COPY apps/api/src/SupportDesk.Infrastructure/SupportDesk.Infrastructure.csproj apps/api/src/SupportDesk.Infrastructure/
COPY apps/api/src/SupportDesk.Presentation/SupportDesk.Presentation.csproj apps/api/src/SupportDesk.Presentation/
RUN dotnet restore apps/api/src/SupportDesk.Presentation/SupportDesk.Presentation.csproj

# Then the source. .editorconfig is read by the build (EnforceCodeStyleInBuild).
COPY .editorconfig ./
COPY apps/api/src/ apps/api/src/
RUN dotnet publish apps/api/src/SupportDesk.Presentation/SupportDesk.Presentation.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    -p:UseAppHost=false

# ---- runtime: ASP.NET Core runtime only, no SDK ----
# Not the chiseled variant: it has no shell, and `docker compose exec api whoami` needs one.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./

# The image's built-in non-root user.
USER $APP_UID

# Plain HTTP only; no certificates in the image. 8080 is the image default, stated explicitly.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "SupportDesk.Presentation.dll"]
