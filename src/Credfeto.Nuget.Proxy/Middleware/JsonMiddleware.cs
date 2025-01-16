using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Credfeto.Nuget.Proxy.Config;
using Credfeto.Nuget.Proxy.Extensions;
using Credfeto.Nuget.Proxy.Middleware.LoggingExtensions;
using Credfeto.Nuget.Proxy.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Credfeto.Nuget.Proxy.Middleware;

public sealed class JsonMiddleware
{
    private readonly ProxyServerConfig _config;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<JsonMiddleware> _logger;
    private readonly RequestDelegate _next;

    public JsonMiddleware(RequestDelegate next, ProxyServerConfig config, IHttpClientFactory httpClientFactory, ILogger<JsonMiddleware> logger)
    {
        this._next = next;
        this._config = config;
        this._httpClientFactory = httpClientFactory;
        this._logger = logger;
    }

    public static string ClientName => "UpStreamJson";

    public async Task InvokeAsync(HttpContext context)
    {
        if (StringComparer.Ordinal.Equals(x: context.Request.Method, y: "GET") && context.Request.Path.HasValue)
        {
            if (StringComparer.OrdinalIgnoreCase.Equals(context.Request.Path.Value, "/v3/index.json"))
            {
                await this.UpstreamIndexAsync(context: context, cancellationToken: context.RequestAborted);
                return;
            }
            if (context.Request.Path.Value.EndsWith(value: ".json", comparisonType: StringComparison.OrdinalIgnoreCase))
            {
                await this.GetFromUpstreamAsync(context: context, cancellationToken: context.RequestAborted);

                return;
            }
        }

        await this._next(context);
    }
#if OVERRIDE_INDEX
    const string INDEX_RESOURCES = @"{
  ""version"": ""3.0.0"",
  ""resources"": [
    {
      ""@id"": ""$$PUBLICURI$$/api/v2/package"",
      ""@type"": ""PackagePublish/2.0.0""
    },
    {
      ""@id"": ""$$PUBLICURI$$/api/v2/symbol"",
      ""@type"": ""SymbolPackagePublish/4.9.0""
    },
    {
      ""@id"": ""$$PUBLICURI$$/v3/search"",
      ""@type"": ""SearchQueryService""
    },
    {
      ""@id"": ""$$PUBLICURI$$/v3/search"",
      ""@type"": ""SearchQueryService/3.0.0-beta""
    },
    {
      ""@id"": ""$$PUBLICURI$$/v3/search"",
      ""@type"": ""SearchQueryService/3.0.0-rc""
    },
    {
      ""@id"": ""$$PUBLICURI$$/v3/registration"",
      ""@type"": ""RegistrationsBaseUrl""
    },
    {
      ""@id"": ""$$PUBLICURI$$/v3/registration"",
      ""@type"": ""RegistrationsBaseUrl/3.0.0-rc""
    },
    {
      ""@id"": ""$$PUBLICURI$$/v3/registration"",
      ""@type"": ""RegistrationsBaseUrl/3.0.0-beta""
    },
    {
      ""@id"": ""$$PUBLICURI$$/v3/package"",
      ""@type"": ""PackageBaseAddress/3.0.0""
    },
    {
      ""@id"": ""$$PUBLICURI$$/v3/autocomplete"",
      ""@type"": ""SearchAutocompleteService""
    },
    {
      ""@id"": ""$$PUBLICURI$$/v3/autocomplete"",
      ""@type"": ""SearchAutocompleteService/3.0.0-rc""
    },
    {
      ""@id"": ""$$PUBLICURI$$/v3/autocomplete"",
      ""@type"": ""SearchAutocompleteService/3.0.0-beta""
    }
  ]
}";
#endif


    private async Task UpstreamIndexAsync(HttpContext context, CancellationToken cancellationToken)
    {
        Uri requestUri = this.GetRequestUri(context);

        HttpResponseMessage result = await this.ReadUpstreamAsync(cancellationToken: cancellationToken, requestUri: requestUri);

        if (result.StatusCode != HttpStatusCode.OK)
        {
            await this.UpstreamFailedAsync(context: context, requestUri: requestUri, result: result, cancellationToken: cancellationToken);

            return;
        }

        string json  = await result.Content.ReadAsStringAsync(cancellationToken: cancellationToken);

        NugetResources? data = JsonSerializer.Deserialize<NugetResources>(json, AppJsonContexts.Default.NugetResources);

        if (data is null)
        {
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            return;
        }

        context.Response.StatusCode = (int)HttpStatusCode.OK;

    }

    private Task UpstreamFailedAsync(HttpContext context, Uri requestUri, HttpResponseMessage result, CancellationToken cancellationToken)
    {
        this._logger.UpstreamFailed(upstream: requestUri, statusCode: result.StatusCode);
        context.Response.StatusCode = (int)result.StatusCode;
        return result.Content.CopyToAsync(stream: context.Response.Body, cancellationToken: cancellationToken);
    }

    private HttpClient GetClient()
    {
        HttpClient client = this._httpClientFactory.CreateClient(ClientName);
        client.BaseAddress = this._config.UpstreamUrl;

        return client;
    }

    private async Task GetFromUpstreamAsync(HttpContext context, CancellationToken cancellationToken)
    {
        Uri requestUri = this.GetRequestUri(context);
        HttpResponseMessage result = await this.ReadUpstreamAsync(cancellationToken: cancellationToken, requestUri: requestUri);

        if (result.StatusCode != HttpStatusCode.OK)
        {
            await this.UpstreamFailedAsync(context: context, requestUri: requestUri, result: result, cancellationToken: cancellationToken);

            return;
        }

        string json = await result.Content.ReadAsStringAsync(cancellationToken: cancellationToken);
        json = this.ReplaceUrls(json);
        this._logger.UpstreamOk(upstream: requestUri, statusCode: result.StatusCode, length: json.Length);
        await context.Response.WriteAsync(text: json, cancellationToken: cancellationToken);
    }

    private async Task<HttpResponseMessage> ReadUpstreamAsync(Uri requestUri, CancellationToken cancellationToken)
    {
        HttpClient client = this.GetClient();

        HttpResponseMessage result = await client.GetAsync(requestUri: requestUri, cancellationToken: cancellationToken);

        return result;
    }

    private Uri GetRequestUri(HttpContext context)
    {
        Uri requestUri = new(this._config.UpstreamUrl.CleanUri() + context.Request.Path.Value);

        return requestUri;
    }

    private string ReplaceUrls(string json)
    {
        json = json.Replace(this._config.UpstreamUrl.CleanUri(), this._config.PublicUrl.CleanUri(), comparisonType: StringComparison.Ordinal);

        json = json.Replace(oldValue: "https://www.nuget.org", this._config.PublicUrl.CleanUri(), comparisonType: StringComparison.Ordinal);

        return json;
    }
}

