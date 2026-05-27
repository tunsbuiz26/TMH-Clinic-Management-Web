using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TMH.API.Data
{
    /// <summary>
    /// Chỉ dùng cho EF Core CLI (dotnet ef migrations add ...).
    /// Không ảnh hưởng đến runtime — connection string thật lấy từ appsettings.
    /// </summary>
    public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            optionsBuilder.UseSqlServer(
                "Server=(localdb)\\mssqllocaldb;Database=TMH_Dev;Trusted_Connection=True;");
            return new AppDbContext(optionsBuilder.Options);
        }
    }
}
