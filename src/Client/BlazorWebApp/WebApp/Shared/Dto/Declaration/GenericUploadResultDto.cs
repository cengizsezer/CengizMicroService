namespace WebApp.Shared.Dto.Declaration
{
    /// <summary>
    /// FileApiService /uploads (genel yükleme) yanıtı. Id, /download?id= ve /delete?id= için kullanılır.
    /// </summary>
    public class GenericUploadResultDto
    {
        public int Id { get; set; }
        public string Key { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = "application/octet-stream";
        public long Length { get; set; }
    }
}
