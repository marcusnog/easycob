FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore && dotnet tool restore && dotnet tool run dotnet-ef migrations bundle --project apps/api/EasyCob.Core/EasyCob.Core.csproj -o /migrate
RUN dotnet publish apps/api/EasyCob.Api.csproj -c Release -o /app --no-self-contained

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
COPY --from=build /migrate .
USER $APP_UID
ENTRYPOINT ["dotnet", "EasyCob.Api.dll"]
