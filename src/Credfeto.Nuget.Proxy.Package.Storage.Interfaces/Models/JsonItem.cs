using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Credfeto.Nuget.Proxy.Package.Storage.Interfaces.Models;

[DebuggerDisplay("Etag: {Etag} Size: {Size} ContentType: {ContentType}")]
public sealed class JsonItem
{
    [JsonConstructor]
    public JsonItem(string etag, long size, string contentType)
    {
        this.Etag = etag;
        this.Size = size;
        this.ContentType = contentType;
    }

    public string Etag { get; }

    public long Size { get; }

    public string ContentType { get; }
}
