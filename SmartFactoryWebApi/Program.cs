using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SmartFactoryWebApi.Data;
using Microsoft.EntityFrameworkCore.SqlServer;
using SmartFactoryWebApi.Services; // 添加以下 using 指令以修复 UseSqlServer 扩展方法未找到的问题


namespace SmartFactoryWebApi;

internal class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        // DbContext 注册
        // DbContext 注册，读取 appsettings.json 中的连接串
        builder.Services.AddDbContext<WmsDbContext>(options =>
            options.UseSqlServer(
                builder.Configuration.GetConnectionString("DefaultConnection"),
                sql => sql.UseCompatibilityLevel(100)));

        // 业务服务注册
        builder.Services.AddScoped<IPickDetailService, PickDetailService>();
        builder.Services.AddScoped<IEntryDetailService, EntryDetailService>();
        builder.Services.AddScoped<IWMSLightService, WMSLightService>();
        
        // Add services to the container.
        builder.Services.AddControllers();
        // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
        builder.Services.AddOpenApi();

        var app = builder.Build();

        // 允许开发环境 OR 生产环境访问 Swagger
        if (app.Environment.IsDevelopment() || app.Environment.IsProduction())
        {
            app.MapOpenApi(); 
        }

        app.UseAuthorization();

        app.MapControllers();

        app.Run();
    }
}
