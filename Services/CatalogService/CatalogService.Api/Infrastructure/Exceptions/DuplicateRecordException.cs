namespace CatalogService.Api.Infrastructure.Exceptions
{
    public class DuplicateRecordException : Exception
    {
        public string Field { get; }

        public DuplicateRecordException(string field, string message) : base(message)
        {
            Field = field;
        }
    }
}
