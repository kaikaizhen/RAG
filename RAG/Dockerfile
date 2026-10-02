# 此階段用於以快速模式從 VS 執行時 (偵錯設定的預設值)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080


# 此階段是用來組建服務專案
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# 共用 Library 不經 NuGet / BaGet，以原始碼一併建置。
# 建置時以 BuildKit 的具名 context 傳入：docker build --build-context library=<Library 資料夾> ...
COPY --from=library Directory.Build.props Directory.Packages.props /library/
COPY --from=library src/ /library/src/
ENV LibraryRoot=/library/

COPY NuGet.Config Directory.Build.props ./

COPY ["RAG/RAG.csproj", "RAG/"]
COPY ["RAG.SpaceRuntime/RAG.SpaceRuntime.csproj", "RAG.SpaceRuntime/"]
COPY ["RAG.SkillRuntime/RAG.SkillRuntime.csproj", "RAG.SkillRuntime/"]
COPY ["RAG.ToolRuntime/RAG.ToolRuntime.csproj", "RAG.ToolRuntime/"]
RUN dotnet restore "./RAG/RAG.csproj"
COPY . .
WORKDIR "/src/RAG"
RUN dotnet build "./RAG.csproj" -c $BUILD_CONFIGURATION -o /app/build --no-restore

# 此階段可用來發佈要複製到最終階段的服務專案
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./RAG.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false --no-restore

# 此階段用於生產環境，或以一般模式從 VS 執行時 (未使用偵錯設定時的預設值)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV ASPNETCORE_URLS=http://+:8006
ENTRYPOINT ["dotnet", "RAG.dll"]
