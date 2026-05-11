FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 5000

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["EasySave.LogServer/EasySave.LogServer.csproj", "EasySave.LogServer/"]
COPY ["EasyLog/EasyLog.csproj", "EasyLog/"]
RUN dotnet restore "EasySave.LogServer/EasySave.LogServer.csproj"

COPY . .
WORKDIR "/src/EasySave.LogServer"
RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Dossier pour les logs centralisés
RUN mkdir -p /app/central-logs

ENV ASPNETCORE_URLS=http://+:5000

ENTRYPOINT ["dotnet", "EasySave.LogServer.dll"]