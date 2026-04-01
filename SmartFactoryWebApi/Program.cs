using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SmartFactoryWebApi.Data;
using Microsoft.EntityFrameworkCore.SqlServer;
using SmartFactoryWebApi.Services; // �������� using ָ�����޸� UseSqlServer ��չ����δ�ҵ�������
using SmartFactoryWebApi.Options;


namespace SmartFactoryWebApi;

internal class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        // DbContext ע��
        // DbContext ע�ᣬ��ȡ appsettings.json �е����Ӵ�
        builder.Services.AddDbContext<WmsDbContext>(options =>
            options.UseSqlServer(
                builder.Configuration.GetConnectionString("DefaultConnection"),
                sql => sql.UseCompatibilityLevel(100)));

        // ҵ�����ע��
        builder.Services.AddScoped<IPickDetailService, PickDetailService>();
        builder.Services.AddScoped<IEntryDetailService, EntryDetailService>();
        builder.Services.AddScoped<IWMSLightService, WMSLightService>();
        builder.Services.Configure<AppUpdateOptions>(builder.Configuration.GetSection("AppUpdate"));
        builder.Services.AddScoped<IAppUpdateService, AppUpdateService>();
        
        // Add services to the container.
        builder.Services.AddControllers();
        // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
        builder.Services.AddOpenApi();

        var app = builder.Build();

        // ������������ OR ������������ Swagger
        if (app.Environment.IsDevelopment() || app.Environment.IsProduction())
        {
            app.MapOpenApi(); 
        }

        app.UseAuthorization();

        app.MapControllers();

        app.Run();
    }
}
