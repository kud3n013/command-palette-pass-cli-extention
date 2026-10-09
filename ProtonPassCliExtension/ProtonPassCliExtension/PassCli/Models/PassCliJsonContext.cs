using System.Text.Json.Serialization;

namespace ProtonPassCliExtension.PassCli.Models;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(VaultListResponse))]
[JsonSerializable(typeof(ItemListResponse))]
[JsonSerializable(typeof(ItemViewResponse))]
[JsonSerializable(typeof(TotpResponse))]
internal sealed partial class PassCliJsonContext : JsonSerializerContext
{
}
