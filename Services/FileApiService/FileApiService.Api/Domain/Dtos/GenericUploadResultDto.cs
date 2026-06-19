namespace FileApiService.Api.Domain.Dtos
{
    /// <summary>
    /// Genel amaçlı dosya yüklemesinin sonucu. Çağıran servis bu kimliği (Id) kendi
    /// veritabanında saklar; görüntülemek için /download?id=Id, silmek için /delete?id=Id kullanır.
    /// </summary>
    public sealed class GenericUploadResultDto
    {
        public int Id { get; init; }
        public string Key { get; init; } = default!;
        public string FileName { get; init; } = default!;
        public string ContentType { get; init; } = "application/octet-stream";
        public long Length { get; init; }
    }
}
