FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["src/partial-content-server/partial-content-server.csproj", "partial-content-server/"]
RUN dotnet restore "src/partial-content-server/partial-content-server.csproj"
COPY . .
WORKDIR "/src/partial-content-server"
RUN dotnet build "partial-content-server.csproj" -c Release -o /app/build --no-restore

FROM build AS publish
RUN dotnet publish "partial-content-server.csproj" -c Release -o /app/publish --no-build --no-restore

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "partial-content-server.dll"]
