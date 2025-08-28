using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Infrastructure.Persistence;

namespace Infrastructure
{
    public class BattleTanksDbContextFactory : IDesignTimeDbContextFactory<BattleTanksDbContext>
    {
        public BattleTanksDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<BattleTanksDbContext>();

            optionsBuilder.UseNpgsql(
                "Host=localhost;Port=5432;Database=battle_tanks1;Username=battleuser;Password=battlepass;Pooling=true;MinPoolSize=0;MaxPoolSize=100"
            );

            return new BattleTanksDbContext(optionsBuilder.Options);
        }
    }
}
