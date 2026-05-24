FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY NuGet.Config ./
COPY MomCare.sln ./
COPY src/MomCare.Api/MomCare.Api.csproj src/MomCare.Api/
COPY src/MomCare.Application/MomCare.Application.csproj src/MomCare.Application/
COPY src/MomCare.Domain/MomCare.Domain.csproj src/MomCare.Domain/
COPY src/MomCare.Infrastructure/MomCare.Infrastructure.csproj src/MomCare.Infrastructure/

RUN dotnet restore src/MomCare.Api/MomCare.Api.csproj

COPY . ./
RUN dotnet publish src/MomCare.Api/MomCare.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish ./

ENV ASPNETCORE_URLS=http://+:10000
ENV DOTNET_EnableDiagnostics=0

EXPOSE 10000

USER app

ENTRYPOINT ["dotnet", "MomCare.Api.dll"]
