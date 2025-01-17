using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using Credfeto.Date;
using Credfeto.Extensions.Linq;
using Credfeto.Nuget.Proxy.Config;
using Credfeto.Nuget.Proxy.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Configuration;
using Serilog.Core;

namespace Credfeto.Nuget.Proxy.Helpers;

internal static class ServerStartup
{
    private const int HTTP_PORT = 5166;
    private const int HTTPS_PORT = 5167;
    private const int H2_PORT = 0;

    public static void SetThreads(int minThreads)
    {
        ThreadPool.GetMinThreads(out int minWorker, out int minIoc);
        Console.WriteLine($"Min worker threads {minWorker}, Min IOC threads {minIoc}");

        if (minWorker < minThreads && minIoc < minThreads)
        {
            Console.WriteLine($"Setting min worker threads {minThreads}, Min IOC threads {minThreads}");
            ThreadPool.SetMinThreads(workerThreads: minThreads, completionPortThreads: minThreads);
        }
        else if (minWorker < minThreads)
        {
            Console.WriteLine($"Setting min worker threads {minThreads}, Min IOC threads {minIoc}");
            ThreadPool.SetMinThreads(workerThreads: minThreads, completionPortThreads: minIoc);
        }
        else if (minIoc < minThreads)
        {
            Console.WriteLine($"Setting min worker threads {minWorker}, Min IOC threads {minThreads}");
            ThreadPool.SetMinThreads(workerThreads: minWorker, completionPortThreads: minThreads);
        }

        ThreadPool.GetMaxThreads(out int maxWorker, out int maxIoc);
        Console.WriteLine($"Max worker threads {maxWorker}, Max IOC threads {maxIoc}");
    }

    public static WebApplication CreateApp(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(args);

        string configPath = ApplicationConfigLocator.ConfigurationFilesPath;

        IConfigurationRoot config = LoadConfiguration(configPath);

        ProxyServerConfig appConfig = LoadConfig(config);
        builder.Services.AddSingleton(appConfig)
               .AddDate()
               .AddJsonClient(appConfig)
               .AddNupkgClient(appConfig);

        builder.Host.UseWindowsService()
               .UseSystemd();
        builder.WebHost.UseKestrel(options: options => SetKestrelOptions(options: options, httpPort: HTTP_PORT, httpsPort: HTTPS_PORT, h2Port: H2_PORT, configurationFiledPath: configPath))
               .UseSetting(key: WebHostDefaults.SuppressStatusMessagesKey, value: "True")
               .ConfigureLogging((_, logger) => ConfigureLogging(logger));

        return builder.Build();
    }

    private static ProxyServerConfig LoadConfig(IConfigurationRoot configuration)
    {
        IReadOnlyList<Uri> upstream = [.. configuration
                                          .GetSection("Proxy:UpstreamUrl")
                                          .GetChildren()
                                          .Select(x => Uri.TryCreate(x.Value, UriKind.Absolute, out Uri? uri) ? uri : null)
                                          .RemoveNulls()
                         ];

        if (upstream.Count == 0)
        {
            throw new UnreachableException("Proxy:UpstreamUrl not provided");
        }

        return new(upstream,
                   new(configuration["Proxy:PublicUrl"] ?? throw new UnreachableException("Proxy:PublicUrl not provided")),
                   configuration["Proxy:Packages"] ?? ApplicationConfigLocator.ConfigurationFilesPath);
    }

    [SuppressMessage(category: "Microsoft.Reliability", checkId: "CA2000:DisposeObjectsBeforeLosingScope", Justification = "Lives for program lifetime")]
    [SuppressMessage(category: "SmartAnalyzers.CSharpExtensions.Annotations", checkId: "CSE007:DisposeObjectsBeforeLosingScope", Justification = "Lives for program lifetime")]
    private static void ConfigureLogging(ILoggingBuilder logger)
    {
        logger.ClearProviders()
              .AddSerilog(CreateLogger(), dispose: true);
    }

    private static Logger CreateLogger()
    {
        return new LoggerConfiguration().Enrich.WithDemystifiedStackTraces()
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

        return Debugger.IsAttached
            ? writeTo.Debug()
            : writeTo.Console();
    }

    private static IConfigurationRoot LoadConfiguration(string configPath)
    {
        return new ConfigurationBuilder().SetBasePath(configPath)
                                         .AddJsonFile(path: "appsettings.json", optional: false, reloadOnChange: false)
                                         .AddJsonFile(path: "appsettings-local.json", optional: true, reloadOnChange: false)
                                         .AddEnvironmentVariables()
                                         .Build();
    }

    private static void SetH1ListenOptions(ListenOptions listenOptions)
    {
        listenOptions.Protocols = HttpProtocols.Http1;
    }

    private static void SetH2ListenOptions(ListenOptions listenOptions)
    {
        listenOptions.Protocols = HttpProtocols.Http2;
    }

    private static void SetHttpsListenOptions(ListenOptions listenOptions, string configurationFiledPath)
    {
        string certFile = Path.Combine(path1: configurationFiledPath, path2: "server.pfx");

        if (!File.Exists(certFile))
        {
            listenOptions.Protocols = HttpProtocols.Http1;
            listenOptions.UseHttps();
        }
        else
        {
            listenOptions.Protocols = HttpProtocols.Http1AndHttp2AndHttp3;
            listenOptions.UseHttps(fileName: certFile);
        }
    }

    private static void SetKestrelOptions(KestrelServerOptions options, int httpPort, int httpsPort, int h2Port, string configurationFiledPath)
    {
        options.DisableStringReuse = false;
        options.AllowSynchronousIO = false;

        options.AddServerHeader = false;
        options.Limits.MinResponseDataRate = null;
        options.Limits.MinRequestBodyDataRate = null;

        if (httpsPort != 0)
        {
            Console.WriteLine($"Listening on HTTPS port: {httpsPort}");
            options.Listen(address: IPAddress.Any, port: httpsPort, configure: o => SetHttpsListenOptions(listenOptions: o, configurationFiledPath: configurationFiledPath));
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