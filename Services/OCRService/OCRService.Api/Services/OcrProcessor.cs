using Google.Cloud.Vision.V1;

namespace OCRService.Api.Services;

public class OcrProcessor
{
    private readonly string _credentialsPath;

    public OcrProcessor(IConfiguration configuration)
    {
        _credentialsPath = configuration["GoogleVision:CredentialsPath"];

        // Ortam değişkeni olarak atıyoruz ki Vision API kullanabilsin
        Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", _credentialsPath);
    }

    public async Task<string> ExtractTextAsync(Stream imageStream)
    {
        var client = await ImageAnnotatorClient.CreateAsync();

        byte[] imageBytes;
        using (var ms = new MemoryStream())
        {
            await imageStream.CopyToAsync(ms);
            imageBytes = ms.ToArray();
        }

        var image = Image.FromBytes(imageBytes);

        var response = await client.DetectTextAsync(image);

        // İlk sonuç full metin olarak döner (response[0] full metin, geri kalanı bloklar)
        return response.Count > 0 ? response[0].Description : string.Empty;
    }
}
