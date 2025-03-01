using FileUploader.Db;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FileUploader.Tests.Helpers
{
    public class CustomAppFactory : WebApplicationFactory<Program>
    {
        public TestFixture TestFixture;

        public CustomAppFactory(TestFixture testFixture)
        {
            TestFixture = testFixture;
        }

        public IWebHostEnvironment GetHostEnvironment()
        {
            using (var scope = Services.CreateScope())
            {
                return scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
            }
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                ServiceDescriptor? dbContextDescriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
                services.Remove(dbContextDescriptor!);

                ServiceDescriptor? ctx = services.SingleOrDefault(d => d.ServiceType == typeof(AppDbContext));
                services.Remove(ctx!);

                // add back the container-based dbContext
                services.AddDbContext<AppDbContext>(opts =>
                    opts.UseNpgsql(this.TestFixture.DatabaseConnectionString));
            });
        }
    }
}
