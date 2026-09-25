using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace RAG.Models.Entities;

/// <summary>
/// 僅供 dotnet ef（migration 產生 / 套用）使用；執行期仍由 RegistExtension.AddDb 建立 DbContext。
/// 連線字串取自環境變數 ConnectionStrings__DefaultConnection；產生 migration / script 時不需連線。
/// 資料庫版本取自 RAG_EF_SERVER_VERSION（預設 MariaDB 10.11，與既有 schema 的 latin1_swedish_ci / current_timestamp() 相符）。
/// </summary>
public class WenDaHubContextFactory : IDesignTimeDbContextFactory<WenDaHubContext>
{
    public WenDaHubContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Server=localhost;Database=design_time_only";
        var serverVersion = ServerVersion.Parse(Environment.GetEnvironmentVariable("RAG_EF_SERVER_VERSION") ?? "10.11.0-mariadb");

        var options = new DbContextOptionsBuilder<WenDaHubContext>()
            .UseMySql(connectionString, serverVersion)
            .Options;

        return new WenDaHubContext(options);
    }
}
