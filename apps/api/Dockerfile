# syntax=docker/dockerfile:1
#
# The API image (P0-084), pushed by CI to ghcr.io/razibur/early-eagles-api
# (ADR-0010). Build context: the repo root, because the analyzers are
# configured by two .editorconfig files (repo root, inherited by apps/api) and
# the image must build under exactly the rules local and CI builds use.
#
#   docker build -f apps/api/Dockerfile -t early-eagles-api \
#     --build-arg SOURCE_REVISION=$(git rev-parse HEAD) .
#
# Runtime is the chiseled ASP.NET image: no shell, no package manager, a
# non-root `app` user by default, listening on 8080 (EE_API_PORT=8080 in the
# deploy runbook). There's nothing in it to exec into; diagnose through logs
# and /health, /ready.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Build settings and project files first, so the restore layer is reused
# until a dependency changes.
COPY .editorconfig ./
COPY apps/api/.editorconfig apps/api/Directory.Build.props apps/api/stylecop.json apps/api/
COPY apps/api/src/EarlyEagles.Domain/EarlyEagles.Domain.csproj apps/api/src/EarlyEagles.Domain/
COPY apps/api/src/EarlyEagles.Application/EarlyEagles.Application.csproj apps/api/src/EarlyEagles.Application/
COPY apps/api/src/EarlyEagles.Infrastructure/EarlyEagles.Infrastructure.csproj apps/api/src/EarlyEagles.Infrastructure/
COPY apps/api/src/EarlyEagles.Api/EarlyEagles.Api.csproj apps/api/src/EarlyEagles.Api/
RUN dotnet restore apps/api/src/EarlyEagles.Api/EarlyEagles.Api.csproj

COPY apps/api/src/ apps/api/src/

# No .git in the build context, so the commit /health reports
# (InformationalVersion 1.0.0+<sha>) is passed in.
ARG SOURCE_REVISION=unknown
RUN dotnet publish apps/api/src/EarlyEagles.Api/EarlyEagles.Api.csproj \
      --configuration Release --no-restore --output /app \
      -p:SourceRevisionId=${SOURCE_REVISION} -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS runtime
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
# The chiseled image already defaults to the non-root `app` user; stating it
# keeps that true if the base image changes, and satisfies the Semgrep rule.
USER $APP_UID
ENTRYPOINT ["dotnet", "EarlyEagles.Api.dll"]
