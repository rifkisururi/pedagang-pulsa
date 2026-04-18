# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["PedagangPulsa.Api/PedagangPulsa.Api.csproj", "PedagangPulsa.Api/"]
COPY ["PedagangPulsa.Application/PedagangPulsa.Application.csproj", "PedagangPulsa.Application/"]
COPY ["PedagangPulsa.Infrastructure/PedagangPulsa.Infrastructure.csproj", "PedagangPulsa.Infrastructure/"]
COPY ["PedagangPulsa.Domain/PedagangPulsa.Domain.csproj", "PedagangPulsa.Domain/"]
RUN dotnet restore "PedagangPulsa.Api/PedagangPulsa.Api.csproj"

COPY . .
WORKDIR "/src/PedagangPulsa.Api"
RUN dotnet publish "PedagangPulsa.Api.csproj" -c $BUILD_CONFIGURATION \
    -o /app/publish \
    /p:UseAppHost=false \
    /p:DebugType=none \
    /p:DebugSymbols=false

# Runtime stage - Alpine for smallest image (~85MB)
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
RUN apk add --no-cache ca-certificates
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "PedagangPulsa.Api.dll"]
