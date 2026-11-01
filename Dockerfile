# SDK и runtime зафиксированы по версии и digest официального MCR.
FROM mcr.microsoft.com/dotnet/sdk:10.0.401@sha256:e70cdb7f80b0348f5cb85f19a8f670fca061f033d57eed12fa003d58b0e06317 AS build
WORKDIR /source

COPY global.json Directory.Build.props ./
COPY src/SampleApi/SampleApi.csproj src/SampleApi/packages.lock.json src/SampleApi/
RUN dotnet restore src/SampleApi/SampleApi.csproj --locked-mode

COPY src/SampleApi/ src/SampleApi/
RUN dotnet publish src/SampleApi/SampleApi.csproj --configuration Release --no-restore --output /app -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0.12@sha256:222759b391a1aaf241166672c8f99b2d4ada452e7b5319f3c6e8f265a37b5ad4 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
COPY --from=build /app ./
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "SampleApi.dll"]
