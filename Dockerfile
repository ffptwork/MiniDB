FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY MiniDB.Server/MiniDB.Server.csproj MiniDB.Server/
COPY MiniDB.Core/MiniDB.Core.csproj MiniDB.Core/
RUN dotnet restore MiniDB.Server/MiniDB.Server.csproj

COPY MiniDB.Server/ MiniDB.Server/
COPY MiniDB.Core/ MiniDB.Core/
RUN dotnet publish MiniDB.Server/MiniDB.Server.csproj -c Release -o /out --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /out/ ./
ENTRYPOINT ["dotnet", "MiniDB.Server.dll"]
