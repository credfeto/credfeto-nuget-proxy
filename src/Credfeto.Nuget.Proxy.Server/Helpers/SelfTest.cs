using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Nuget.Proxy.Models.Config;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Credfeto.Nuget.Proxy.Server.Helpers;

public static class SelfTest
{
    private const string SELF_TEST_SWITCH = "--self-test";
    private const int SUCCESS_EXIT_CODE = 0;
    private const int FAILURE_EXIT_CODE = 1;

    public static bool IsSelfTest(IReadOnlyList<string> args)
    {
        return args.Contains(value: SELF_TEST_SWITCH, comparer: StringComparer.Ordinal);
    }

    public static async ValueTask<int> RunAsync(
        IReadOnlyList<string> args,
        string configPath,
        TextWriter output,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await CheckAsync(args: args, configPath: configPath);
        }
        catch (Exception exception)
        {
            return await WriteResultAsync(
                output: output,
                message: "Self-test failed: " + ToSingleLine(exception.Message),
                exitCode: FAILURE_EXIT_CODE,
                cancellationToken: cancellationToken
            );
        }

        return await WriteResultAsync(
            output: output,
            message: "Self-test passed",
            exitCode: SUCCESS_EXIT_CODE,
            cancellationToken: cancellationToken
        );
    }

    private static async ValueTask CheckAsync(IReadOnlyList<string> args, string configPath)
    {
        LoadGlobalisation();
        LoadCryptography();

        string[] hostArgs = [.. args.Where(arg => !StringComparer.Ordinal.Equals(x: arg, y: SELF_TEST_SWITCH))];

        await using (WebApplication app = ServerStartup.CreateSelfTestApp(args: hostArgs, configPath: configPath))
        {
            _ = app.Services.GetRequiredService<IOptions<ProxyServerConfig>>().Value;
        }
    }

    private static void LoadGlobalisation()
    {
        CultureInfo culture = CultureInfo.CurrentCulture;

        _ = culture.CompareInfo.Compare(string1: "self", string2: "TEST", options: CompareOptions.IgnoreCase);
        _ = "self-test".ToUpper(culture);
    }

    private static void LoadCryptography()
    {
        _ = SHA256.HashData(Encoding.UTF8.GetBytes(SELF_TEST_SWITCH));
        _ = RandomNumberGenerator.GetInt32(toExclusive: int.MaxValue);
    }

    private static string ToSingleLine(string message)
    {
        return message.ReplaceLineEndings(" ");
    }

    private static async ValueTask<int> WriteResultAsync(
        TextWriter output,
        string message,
        int exitCode,
        CancellationToken cancellationToken
    )
    {
        await output.WriteLineAsync(buffer: message.AsMemory(), cancellationToken: cancellationToken);

        return exitCode;
    }
}
