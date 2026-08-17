FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore
COPY HostelSystem.sln .
COPY src/HostelSystem.Domain/*.csproj src/HostelSystem.Domain/
COPY src/HostelSystem.Application/*.csproj src/HostelSystem.Application/
COPY src/HostelSystem.Infrastructure/*.csproj src/HostelSystem.Infrastructure/
COPY src/HostelSystem.Identity/*.csproj src/HostelSystem.Identity/
COPY src/HostelSystem.Api/*.csproj src/HostelSystem.Api/
RUN dotnet restore src/HostelSystem.Api/HostelSystem.Api.csproj

# Build
COPY . .
RUN dotnet publish src/HostelSystem.Api/HostelSystem.Api.csproj -c Release -o /app

# Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "HostelSystem.Api.dll"]
