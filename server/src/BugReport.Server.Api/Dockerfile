FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
RUN apt-get update && apt-get install -y clang zlib1g-dev && rm -rf /var/lib/apt/lists/*

COPY server/src/BugReport.Server.Api/BugReport.Server.Api.csproj BugReport.Server.Api/
RUN dotnet restore BugReport.Server.Api/BugReport.Server.Api.csproj

COPY server/src/BugReport.Server.Api/ BugReport.Server.Api/
WORKDIR /src/BugReport.Server.Api
RUN dotnet publish BugReport.Server.Api.csproj -c Release -r linux-x64 -o /app/publish -p:PublishAot=true

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-noble-chiseled
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["./BugReport.Server.Api"]
