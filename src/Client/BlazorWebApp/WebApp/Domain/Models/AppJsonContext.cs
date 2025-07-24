using System.Text.Json.Serialization;
using WebApp.Domain.Models;
using WebApp.Domain.Models.Catalog;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(PaginatedItemsViewModel<Expense>))]
[JsonSerializable(typeof(Expense))]
public partial class AppJsonContext : JsonSerializerContext
{
}
