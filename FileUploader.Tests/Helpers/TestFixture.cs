using FileUploader.Db;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace FileUploader.Tests.Helpers
{
    public class TestFixture : IAsyncLifetime
    {
        private AppDbContext? m_dbContext;

        private readonly PostgreSqlContainer _dbContainer;

        public TestFixture()
        {
            _dbContainer = new PostgreSqlBuilder()
                .WithDatabase("testdb")
                .WithUsername("postgres")
                .WithPassword("postgres")
                .Build();
        }

        public string DatabaseConnectionString => _dbContainer.GetConnectionString();
        public AppDbContext AppDbContext => m_dbContext;

        public async Task InitializeAsync()
        {
            await _dbContainer.StartAsync();

            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(DatabaseConnectionString);
            m_dbContext = new AppDbContext(optionsBuilder.Options);
            await m_dbContext.Database.MigrateAsync();
        }

        public async Task DisposeAsync()
        {
            await _dbContainer.DisposeAsync();
        }
    }
}
