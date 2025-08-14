using Google.Cloud.Vision.V1;

namespace OCRService.Api.Services;

public class OcrProcessor
{
    private readonly string _credentialsPath;
    private readonly Lazy<Task<ImageAnnotatorClient>> _clientLazy;

    public OcrProcessor(IConfiguration configuration)
    {
        // 1) Env > Config fallback
        _credentialsPath =
            Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS")
            ?? configuration["GoogleVision:CredentialsPath"];

        // 2) Env yoksa, config’ten geleni env’e yaz (tek kaynak)
        if (!string.IsNullOrWhiteSpace(_credentialsPath) &&
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS")))
        {
            Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", _credentialsPath);
        }

        // 3) Client'ı lazy + reuse (tek örnek)
        _clientLazy = new Lazy<Task<ImageAnnotatorClient>>(async () =>
        {
            // İstersen burada da path'i zorla:
            // var builder = new ImageAnnotatorClientBuilder { CredentialsPath = _credentialsPath };
            // return await builder.BuildAsync();

            return await ImageAnnotatorClient.CreateAsync();
        });
    }

    public async Task<string> ExtractTextAsync(Stream imageStream)
    {
        try
        {
            var client = await _clientLazy.Value;

            // Görüntüyü oku
            byte[] imageBytes;
            using (var ms = new MemoryStream())
            {
                await imageStream.CopyToAsync(ms);
                imageBytes = ms.ToArray();
            }
            var image = Image.FromBytes(imageBytes);

            // 4) Belge OCR (tam metin)
            var doc = await client.DetectDocumentTextAsync(image);
            return doc?.Text ?? string.Empty;
        }
        catch (Grpc.Core.RpcException rpcEx)
        {
            // Kimlik / yetki / dosya yolu hataları için daha net bilgi
            // Örn: Status(StatusCode=Unauthenticated, Detail="Invalid Credentials")
            // ya da NotFound vs.
            // Burada kendi logger'ını kullanabilirsin (ILogger<OcrProcessor>)
            Console.Error.WriteLine($"[OCR] RPC error: {rpcEx.Status} - {rpcEx.Status.Detail}");
            return string.Empty;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[OCR] Unexpected error: {ex.Message}");
            return string.Empty;
        }
    }
}
