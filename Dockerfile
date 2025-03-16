FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app
EXPOSE 3000 3001
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY . .

RUN dotnet restore && dotnet build -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false /p:DebugType=portable /p:DebugSymbols=true

FROM publish AS final
WORKDIR /app-run

COPY --from=publish /app/publish .
RUN rm -rf /app && dotnet dev-certs https --clean && \
    dotnet dev-certs https --trust && update-ca-certificates

ENTRYPOINT ["dotnet", "CrypticAnalytic.dll"]
