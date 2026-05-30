using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SmartFactoryWebApi.Data;
using Microsoft.EntityFrameworkCore.SqlServer;
using SmartFactoryWebApi.Hubs;
using SmartFactoryWebApi.Options;
using SmartFactoryWebApi.Services;

namespace SmartFactoryWebApi;

/// <summary>
/// 后端应用启动入口，负责注册数据访问、业务服务、控制器和 SignalR Hub。
/// </summary>
internal class Program
{
    /// <summary>
    /// 构建 WebApplication，注册业务依赖并启动 HTTP 与 SignalR 管道。
    /// </summary>
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // 所有业务查询和事务都依赖同一个 WMS 上下文。
        builder.Services.AddDbContext<WmsDbContext>(options =>
            options.UseSqlServer(
                builder.Configuration.GetConnectionString("DefaultConnection"),
                sql => sql.UseCompatibilityLevel(100)));

        builder.Services.AddScoped<IPickDetailService, PickDetailService>();
        builder.Services.AddScoped<IEntryDetailService, EntryDetailService>();
        builder.Services.AddScoped<IWMSLightService, WMSLightService>();
        builder.Services.Configure<AppUpdateOptions>(builder.Configuration.GetSection("AppUpdate"));
        builder.Services.AddScoped<IAppUpdateService, AppUpdateService>();

        builder.Services.AddScoped<IInductionRackApiService, InductionRackApiService>();
        builder.Services.AddScoped<IInductionEntryService, InductionEntryService>();
        builder.Services.AddScoped<IInductionPickService, InductionPickService>();
        builder.Services.AddScoped<IInductionLightService, InductionLightService>();
        builder.Services.AddScoped<IInductionHubContext, InductionHubContext>();

        builder.Services.AddControllers();
        builder.Services.AddOpenApi();
        builder.Services.AddSignalR();

        var app = builder.Build();

        if (app.Environment.IsDevelopment() || app.Environment.IsProduction())
        {
            app.MapOpenApi();
        }

        app.UseAuthorization();
        app.MapControllers();

        // 感应料架回调通过 Hub 推送给各端，路由需要与客户端配置保持一致。
        app.MapHub<InductionHub>("/hubs/induction");

        app.Run();
    }
}
