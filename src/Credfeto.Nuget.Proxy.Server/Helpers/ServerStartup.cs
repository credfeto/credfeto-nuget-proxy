using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using Credfeto.Date;
using Credfeto.Nuget.Proxy.Logic;
using Credfeto.Nuget.Proxy.Middleware;
using Credfeto.Nuget.Proxy.Models.Config;
using Credfeto.Nuget.Proxy.Models.Models;
using Credfeto.Nuget.Proxy.Package.Storage.FileSystem;
using Credfeto.Nuget.Proxy.Server.Helpers.LoggingExtensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Configuration;
using Serilog.Core;

namespace Credfeto.Nuget.Proxy.Server.Helpers;

internal static class ServerStartup
{
#if DEBUG
    private const int HTTP_PORT = 9080;
    private const int HTTPS_PORT = 9081;
    private const int H2_PORT = 0;
#else
    private const int HTTP_PORT = 8080;
    private const int HTTPS_PORT = 8081;
    private const int H2_PORT = 0;
#endif

    public static void SetThreads(int minThreads)
    {
        ThreadPool.GetMinThreads(out int minWorker, out int minIoc);
        Console.WriteLine($"Min worker threads {minWorker}, Min IOC threads {minIoc}");

        int workerThreads = Math.Max(val1: minWorker, val2: minThreads);
        int completionPortThreads = Math.Max(val1: minIoc, val2: minThreads);

        if (workerThreads != minWorker || completionPortThreads != minIoc)
        {
            Console.WriteLine($"Setting min worker threads {workerThreads}, Min IOC threads {completionPortThreads}");
            ThreadPool.SetMinThreads(workerThreads: workerThreads, completionPortThreads: completionPortThreads);
        }

        ThreadPool.GetMaxThreads(out int maxWorker, out int maxIoc);
        Console.WriteLine($"Max worker threads {maxWorker}, Max IOC threads {maxIoc}");
    }

    public static WebApplication CreateApp(string[] args)
    {
        string configPath = ApplicationConfigLocator.ConfigurationFilesPath;

        WebApplication app = WebApplication
            .CreateSlimBuilder(args)
            .ConfigureSettings(configPath)
            .ConfigureServices()
            .ConfigureAppHost()
            .ConfigureWebHost(configPath: configPath)
            .Build();

        WarnIfPlaceholderPublicUrl(app);

        return app;
    }

    private static void WarnIfPlaceholderPublicUrl(WebApplication app)
    {
        ProxyServerConfig config = app.Services.GetRequiredService<IOptions<ProxyServerConfig>>().Value;

        if (ProxyServerConfigValidation.IsPlaceholderPublicUrl(config))
        {
            app.Logger.PlaceholderPublicUrl(config.PublicUrl);
        }
    }

    private static WebApplicationBuilder ConfigureAppHost(this WebApplicationBuilder builder)
    {
        builder.Host.UseWindowsService().UseSystemd();

        return builder;
    }

    private static WebApplicationBuilder ConfigureServices(this WebApplicationBuilder builder)
    {
        IConfigurationSection section = builder.Configuration.GetSection("Proxy");

        builder
            .Services.AddOptions<ProxyServerConfig>()
            .Bind(section)
            .Validate(
                validation: ProxyServerConfigValidation.HasUpstreamUrls,
                failureMessage: "Proxy:UpstreamUrls must contain at least one upstream URL"
            )
            .Validate(
                validation: ProxyServerConfigValidation.UpstreamUrlsAreAbsolute,
                failureMessage: "Proxy:UpstreamUrls must all be absolute http(s) URIs"
            )
            .Validate(
                validation: ProxyServerConfigValidation.PublicUrlIsAbsolute,
                failureMessage: "Proxy:PublicUrl must be an absolute http(s) URI"
            )
            .ValidateOnStart();

        builder
            .Services.AddDate()
            .AddFileSystemStorage()
            .AddLogic()
            .AddMiddleware()
            .ConfigureHttpJsonOptions(options =>
                options.SerializerOptions.TypeInfoResolverChain.Insert(index: 0, item: AppJsonContexts.Default)
            );

        return builder;
    }

    private static WebApplicationBuilder ConfigureWebHost(this WebApplicationBuilder builder, string configPath)
    {
        builder
            .WebHost.UseKestrel(options: options =>
                SetKestrelOptions(
                    options: options,
                    httpPort: HTTP_PORT,
                    httpsPort: HTTPS_PORT,
                    h2Port: H2_PORT,
                    configurationFiledPath: configPath
                )
            )
            .UseSetting(key: WebHostDefaults.SuppressStatusMessagesKey, value: "True")
            .ConfigureLogging((_, logger) => ConfigureLogging(logger));

        return builder;
    }

    [SuppressMessage(
        category: "Microsoft.Reliability",
        checkId: "CA2000:DisposeObjectsBeforeLosingScope",
        Justification = "Lives for program lifetime"
    )]
    [SuppressMessage(
        category: "SmartAnalyzers.CSharpExtensions.Annotations",
        checkId: "CSE007:DisposeObjectsBeforeLosingScope",
        Justification = "Lives for program lifetime"
    )]
    private static void ConfigureLogging(ILoggingBuilder logger)
    {
        logger.ClearProviders().AddSerilog(CreateLogger(), dispose: true);
    }

    private static Logger CreateLogger()
    {
        return new LoggerConfiguration()
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithProcessId()
            .Enrich.WithThreadId()
            .Enrich.WithProperty(name: "ServerVersion", value: VersionInformation.Version)
            .Enrich.WithProperty(name: "ProcessName", value: VersionInformation.Product)
            .WriteToDebuggerAwareOutput()
            .CreateLogger();
    }

    private static LoggerConfiguration WriteToDebuggerAwareOutput(this LoggerConfiguration configuration)
    {
        LoggerSinkConfiguration writeTo = configuration.WriteTo;

        return Debugger.IsAttached ? writeTo.Debug() : writeTo.Console();
    }

    private static WebApplicationBuilder ConfigureSettings(this WebApplicationBuilder builder, string configPath)
    {
        builder.Configuration.Sources.Clear();
        builder
            .Configuration.SetBasePath(configPath)
            .AddJsonFile(path: "appsettings.json", optional: false, reloadOnChange: false)
            .AddJsonFile(path: "appsettings-local.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables();

        return builder;
    }

    private static void SetH1ListenOptions(ListenOptions listenOptions)
    {
        listenOptions.Protocols = HttpProtocols.Http1;
    }

    private static void SetH2ListenOptions(ListenOptions listenOptions)
    {
        listenOptions.Protocols = HttpProtocols.Http2;
    }

    [SuppressMessage(
        category: "Microsoft.Reliability",
        checkId: "CA2000:DisposeObjectsBeforeLosingScope",
        Justification = "Lives for program lifetime"
    )]
    [SuppressMessage(
        category: "SmartAnalyzers.CSharpExtensions.Annotations",
        checkId: "CSE007:DisposeObjectsBeforeLosingScope",
        Justification = "Lives for program lifetime"
    )]
    private static void SetHttpsListenOptions(ListenOptions listenOptions, string certFile)
    {
        listenOptions.Protocols = HttpProtocols.Http1AndHttp2AndHttp3;
        X509Certificate2 cert = X509CertificateLoader.LoadPkcs12FromFile(
            certFile,
            password: null,
            keyStorageFlags: X509KeyStorageFlags.EphemeralKeySet
        );
        listenOptions.UseHttps(cert);
    }

    private static void SetKestrelOptions(
        KestrelServerOptions options,
        int httpPort,
        int httpsPort,
        int h2Port,
        string configurationFiledPath
    )
    {
        options.DisableStringReuse = false;
        options.AllowSynchronousIO = false;

        options.AddServerHeader = false;
        options.Limits.MinResponseDataRate = null;
        options.Limits.MinRequestBodyDataRate = null;

        string certFile = Path.Combine(path1: configurationFiledPath, path2: "server.pfx");

        if (httpsPort != 0 && File.Exists(certFile))
        {
            Console.WriteLine($"Listening on HTTPS port: {httpsPort}");
            options.Listen(
                address: IPAddress.Any,
                port: httpsPort,
                configure: o => SetHttpsListenOptions(listenOptions: o, certFile: certFile)
            );
        }

        if (h2Port != 0)
        {
            Console.WriteLine($"Listening on H2 port: {h2Port}");
            options.Listen(address: IPAddress.Any, port: h2Port, configure: SetH2ListenOptions);
        }

        Console.WriteLine($"Listening on HTTP port: {httpPort}");
        options.Listen(address: IPAddress.Any, port: httpPort, configure: SetH1ListenOptions);
    }
}
