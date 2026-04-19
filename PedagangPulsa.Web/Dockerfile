# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["PedagangPulsa.Web/PedagangPulsa.Web.csproj", "PedagangPulsa.Web/"]
COPY ["PedagangPulsa.Application/PedagangPulsa.Application.csproj", "PedagangPulsa.Application/"]
COPY ["PedagangPulsa.Infrastructure/PedagangPulsa.Infrastructure.csproj", "PedagangPulsa.Infrastructure/"]
COPY ["PedagangPulsa.Domain/PedagangPulsa.Domain.csproj", "PedagangPulsa.Domain/"]
RUN dotnet restore "PedagangPulsa.Web/PedagangPulsa.Web.csproj"

COPY . .
WORKDIR "/src/PedagangPulsa.Web"
RUN dotnet publish "PedagangPulsa.Web.csproj" -c $BUILD_CONFIGURATION \
    -o /app/publish \
    /p:UseAppHost=false \
    /p:DebugType=none \
    /p:DebugSymbols=false

# Runtime stage - Alpine for smallest image
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
RUN apk add --no-cache ca-certificates tzdata icu-libs
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "PedagangPulsa.Web.dll"]
