using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Credfeto.Nuget.Index.Transformer.Interfaces;
using Microsoft.AspNetCore.Http;

namespace Credfeto.Nuget.Proxy.Middleware;

[SuppressMessage(
    category: "Microsoft.Security",
    checkId: "CA3003: Potential Path injection",
    Justification = "Avoided by checking path above"
)]
[SuppressMessage(
    category: "SecurityCodeScan.VS2019",
    checkId: "SCS0018: Potential Path injection",
    Justification = "Avoided by checking path above"
)]
public sealed class NuPkgMiddleware
{
    private readonly INupkgSource _nupkgSource;
    private readonly RequestDelegate _next;

    public NuPkgMiddleware(RequestDelegate next, INupkgSource nupkgSource)
    {
        this._next = next;
        this._nupkgSource = nupkgSource;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (
            StringComparer.Ordinal.Equals(x: context.Request.Method, y: "GET")
            && context.Request.Path.HasValue
        )
        {
            if (
                context.Request.Path.Value.Contains(
                    value: "../",
                    comparisonType: StringComparison.Ordinal
                )
            )
            {
                await this._next(context);

                return;
            }

            string sourcePath = context.Request.Path.Value;

            if (
                await this._nupkgSource.GetFromUpstreamAsync(
                    context: context,
                    path: sourcePath,
                    cancellationToken: context.RequestAborted
                )
            )
            {
                return;
            }
        }

        await this._next(context);
    }
}
