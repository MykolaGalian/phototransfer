using System.Text.Json.Serialization;
using PhotoTransfer.Models;

namespace PhotoTransfer;

[JsonSerializable(typeof(PhotoIndex))]
[JsonSerializable(typeof(PhotoMetadata))]
[JsonSerializable(typeof(PhotoMetadata[]))]
[JsonSerializable(typeof(IndexingProgress))]
[JsonSerializable(typeof(BaseIndex))]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(HashSet<string>))]
internal partial class JsonContext : JsonSerializerContext
{
}