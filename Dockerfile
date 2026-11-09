# Build stage: restores packages first so Docker can reuse that layer when only code changes.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ZuMarket.csproj NuGet.Config ./
RUN dotnet restore ZuMarket.csproj
COPY . .
RUN dotnet publish ZuMarket.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# Runtime stage: only the published app, running as the image's non-root user.
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
# App_Data holds uploaded photos and Data Protection keys. It is created here so the
# named volume mounted over it inherits ownership by the non-root user.
RUN mkdir -p /app/App_Data && chown -R app:app /app/App_Data
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "ZuMarket.dll"]
