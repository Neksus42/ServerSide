using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ServerSideForPC.Configuration;
using ServerSideForPC.Networking;
using ServerSideForPC.Services;

namespace ServerSideForPC;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            ContentRootPath = AppContext.BaseDirectory
        });

        var options = builder.Configuration.GetSection("PcControl").Get<PcControlOptions>() ?? new PcControlOptions();

        builder.WebHost.UseUrls($"http://0.0.0.0:{options.Port}");
        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton<AudioService>();
        builder.Services.AddSingleton<DisplayService>();
        builder.Services.AddSingleton<PowerService>();
        builder.Services.AddSingleton<MediaService>();
        builder.Services.AddSingleton<ProfileService>();
        builder.Services.AddSingleton<PcStateService>();
        builder.Services.AddSingleton<WebSocketGateway>();

        var app = builder.Build();

        app.UseWebSockets(new WebSocketOptions
        {
            KeepAliveInterval = TimeSpan.FromSeconds(20)
        });

        app.Use(async (context, next) =>
        {
            if (context.Request.Path == "/ws")
            {
                var gateway = context.RequestServices.GetRequiredService<WebSocketGateway>();
                await gateway.HandleAsync(context);
                return;
            }

            await next();
        });

        app.MapGet("/health", () => Results.Ok(new
        {
            service = "pc-control",
            machine = Environment.MachineName,
            ok = true
        }));

        // OutputType=WinExe means no console window is created. RunAsync keeps the
        // agent alive entirely in the background until Windows or the user stops it.
        await app.RunAsync();
    }
}
