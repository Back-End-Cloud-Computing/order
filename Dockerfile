FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

COPY Ganjj.Order.sln ./
COPY src/Ganjj.Order.Domain/Ganjj.Order.Domain.csproj src/Ganjj.Order.Domain/
COPY src/Ganjj.Order.Application/Ganjj.Order.Application.csproj src/Ganjj.Order.Application/
COPY src/Ganjj.Order.Infrastructure/Ganjj.Order.Infrastructure.csproj src/Ganjj.Order.Infrastructure/
COPY src/Ganjj.Order.Api/Ganjj.Order.Api.csproj src/Ganjj.Order.Api/
RUN dotnet restore src/Ganjj.Order.Api/Ganjj.Order.Api.csproj

COPY src/ src/
RUN dotnet publish src/Ganjj.Order.Api/Ganjj.Order.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "Ganjj.Order.Api.dll"]
