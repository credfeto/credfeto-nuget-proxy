using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Credfeto.Nuget.Proxy.Package.Storage.Interfaces.Models;

[DebuggerDisplay("Etag: {Etag} Size: {Size}  Content: {Content}")]
public sealed class JsonItem
{
    [JsonConstructor]
    public JsonItem(string etag, long size, string contentType, string content)
    {
        this.Etag = etag;
        this.Size = size;
        this.ContentType = contentType;
        this.Content = content;
    }

    public string Etag { get; }

    public long Size { get; }

    public string ContentType { get; }

    public string Content { get; }
}