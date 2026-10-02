using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace RAG.Models.Entities;

/// <summary>
/// 僅供 dotnet ef（migration 產生 / 套用）使用；執行期仍由 RegistExtension.AddDb 建立 DbContext。
/// 連線字串取自環境變數 ConnectionStrings__DefaultConnection；產生 migration / script 時不需連線。
/// </summary>
public class WenDaHubContextFactory : IDesignTimeDbContextFactory<WenDaHubContext>
{
    public WenDaHubContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Server=localhost;Database=design_time_only";

        var options = new DbContextOptionsBuilder<WenDaHubContext>()
            .UseMySQL(connectionString)
            .Options;

        return new WenDaHubContext(options);
    }
}
