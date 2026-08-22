# --- Build stage ---
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/*.csproj ./src/
RUN dotnet restore ./src/GoRide.IdentityAuth.csproj
COPY src/. ./src/
RUN dotnet publish ./src/GoRide.IdentityAuth.csproj -c Release -o /app

# --- Runtime stage ---
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "GoRide.IdentityAuth.dll"]