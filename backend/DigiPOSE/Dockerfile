# Multi-stage Dockerfile for DigiPOSE (.NET 10.0 ASP.NET Core)
FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src
COPY ["DigiPOSE.csproj", "./"]
RUN dotnet restore "DigiPOSE.csproj"
COPY . .
RUN dotnet build "DigiPOSE.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "DigiPOSE.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Mount persistent volume for uploaded user images
VOLUME ["/app/wwwroot/uploads"]

ENTRYPOINT ["dotnet", "DigiPOSE.dll"]
