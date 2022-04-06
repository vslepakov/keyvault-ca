#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["KeyVaultCA.Web/KeyVaultCA.Web.csproj", "KeyVaultCA.Web/"]
COPY ["KeyVaultCa.Core/KeyVaultCa.Core.csproj", "KeyVaultCa.Core/"]
RUN dotnet restore "KeyVaultCA.Web/KeyVaultCA.Web.csproj"
COPY . .
WORKDIR "/src/KeyVaultCA.Web"
RUN dotnet build "KeyVaultCA.Web.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "KeyVaultCA.Web.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "KeyVaultCA.Web.dll"]
