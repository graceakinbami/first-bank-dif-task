FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY NovaPay.Api/NovaPay.Api.csproj NovaPay.Api/
RUN dotnet restore NovaPay.Api/NovaPay.Api.csproj

COPY NovaPay.Api/ NovaPay.Api/
RUN dotnet publish NovaPay.Api/NovaPay.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .

# Render injects PORT at runtime; Kestrel must bind 0.0.0.0:$PORT rather than a fixed port.
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
ENTRYPOINT ["/bin/sh", "-c", "ASPNETCORE_URLS=http://0.0.0.0:${PORT:-8080} exec dotnet NovaPay.Api.dll"]
