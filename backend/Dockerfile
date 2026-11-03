FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY KitsuneHrm.Api.csproj ./
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080 ASPNETCORE_ENVIRONMENT=Production
COPY --from=build /app/publish .
RUN useradd -r -u 10001 kitsune && chown -R kitsune /app
USER kitsune
EXPOSE 8080
ENTRYPOINT ["dotnet", "KitsuneHrm.Api.dll"]
