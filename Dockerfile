#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:9.0-bookworm-slim-amd64 AS base
USER app
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
COPY "." "/src"
WORKDIR "/src"
RUN dotnet build "./src/Scheduler/Scheduler.csproj" -c $BUILD_CONFIGURATION -o /app/build -r linux-x64

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./src/Scheduler/Scheduler.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false -r linux-x64

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Scheduler.dll"]
