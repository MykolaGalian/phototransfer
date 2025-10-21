using System.Text.Json.Serialization;
using PhotoTransfer.Models;

namespace PhotoTransfer;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(PhotoIndex))]
[JsonSerializable(typeof(PhotoMetadata))]
[JsonSerializable(typeof(PhotoMetadata[]))]
[JsonSerializable(typeof(DateSource))]
[JsonSerializable(typeof(DateSource[]))]
[JsonSerializable(typeof(List<DateSource>))]
[JsonSerializable(typeof(IndexingProgress))]
[JsonSerializable(typeof(BaseIndex))]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(HashSet<string>))]
public partial class JsonContext : JsonSerializerContext
{
}