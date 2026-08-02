# 請參閱 https://aka.ms/customizecontainer 了解如何自訂您的偵錯容器，以及 Visual Studio 如何使用此 Dockerfile 來組建您的映像，以加快偵錯速度。

# 此階段用於以快速模式從 VS 執行時 (偵錯設定的預設值)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080


# 此階段是用來組建服務專案
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# 私有 NuGet 來源（BaGet），請以 --build-arg NUGET_PRIVATE_FEED=... 傳入
ARG NUGET_PRIVATE_FEED
ENV NUGET_PRIVATE_FEED=${NUGET_PRIVATE_FEED}

COPY NuGet.Config .

RUN echo "🔍 Checking private NuGet feed connectivity..." && \
    apt-get update -y && apt-get install -y curl && \
    if [ -z "$NUGET_PRIVATE_FEED" ]; then \
        echo "❌ ERROR: NUGET_PRIVATE_FEED is not set"; \
        exit 1; \
    fi && \
    if curl -s --head --request GET "$NUGET_PRIVATE_FEED" | grep "200 OK" > /dev/null; then \
        echo "✅ Private NuGet feed is reachable!"; \
    else \
        echo "❌ ERROR: Cannot reach private NuGet feed"; \
        exit 1; \
    fi


COPY ["RAG/RAG.csproj", "RAG/"]
RUN dotnet restore "./RAG/RAG.csproj"
COPY . .
WORKDIR "/src/RAG"
RUN dotnet build "./RAG.csproj" -c $BUILD_CONFIGURATION -o /app/build

# 此階段可用來發佈要複製到最終階段的服務專案
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./RAG.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# 此階段用於生產環境，或以一般模式從 VS 執行時 (未使用偵錯設定時的預設值)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV ASPNETCORE_URLS=http://+:8006
ENTRYPOINT ["dotnet", "RAG.dll"]