
using iText.Kernel.Pdf;
using iText.Kernel.Utils;
using Microsoft.Extensions.Options;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Xobject;
using iText.Kernel.Utils;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;
using Ghostscript.NET;
using Ghostscript.NET.Processor;
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddAntiforgery();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
   c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
   {
       Title = "PDF Compression Service",
       Version = "v1",
       Description = "A service to compress PDF files using iTextSharp."
   });
});
builder.Configuration.AddJsonFile("appsettings.json");
builder.Services.Configure<PdfSettings>(builder.Configuration.GetSection("PdfCompression"));
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "PDF Compression Service V1");
        c.RoutePrefix = string.Empty; // Set Swagger UI at the app's root
        c.RoutePrefix = "swagger";
    });
}
app.UseAntiforgery();


app.MapGet("/test", () => "PDF service is working!");

// Endpoint tp compress PDF files
// This endpoint accepts a PDF file, compresses it, and returns the compressed file.
// The compression is done using iTextSharp library.
app.MapPost("/compress-pdf", async (IFormFile file, IOptions<PdfSettings> settings) =>
{
    var pdfSettings = settings.Value;

    if (!IsValidFile(file, pdfSettings, out var validationError))
        return Results.BadRequest(validationError);

    try
    {
        using var inputStream = file.OpenReadStream();
        var compressedBytes = CompressPdfWithGhostscript(inputStream, "screen");

        var compressionRatio = Math.Round((1.0 - (double)compressedBytes.Length / file.Length) * 100, 2);
        Console.WriteLine($"Original: {file.Length} bytes, Compressed: {compressedBytes.Length} bytes, Saved: {compressionRatio}%");

        return Results.File(compressedBytes, "application/pdf", $"compressed_{file.FileName}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Compression error: {ex.Message}");
        return Results.Problem($"Error: {ex.Message}");
    }

    static bool IsValidFile(IFormFile file, PdfSettings settings, out string error)
    {
        error = "";

        if (file == null || file.Length == 0)
        {
            error = "No file uploaded.";
            return false;
        }

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var maxSize = settings.MaxFileSizeInMB * 1024 * 1024;
        var minSize = settings.MinFileSizeInMB * 1024 * 1024;

        if (file.Length > maxSize)
        {
            error = $"File size exceeds the maximum of {settings.MaxFileSizeInMB} MB.";
            return false;
        }

        if (file.Length < minSize)
        {
            error = $"File size is below the minimum of {settings.MinFileSizeInMB} MB.";
            return false;
        }

        if (!settings.AllowedExtensions.Contains(ext))
        {
            error = $"File extension '{ext}' is not allowed.";
            return false;
        }

        if (file.ContentType != "application/pdf")
        {
            error = "Only PDF files are allowed.";
            return false;
        }

        return true;
    }

    static byte[] CompressPdfWithGhostscript(Stream inputStream, string pdfSetting)
    {
        var tempInputPath = Path.GetTempFileName() + ".pdf";
        var tempOutputPath = Path.GetTempFileName() + "_compressed.pdf";

        try
        {
            // Save input stream to temp file
            using (var fileStream = File.Create(tempInputPath))
                inputStream.CopyTo(fileStream);

            var gsVersion = GhostscriptVersionInfo.GetLastInstalledVersion(
                GhostscriptLicense.GPL | GhostscriptLicense.AFPL,
                GhostscriptLicense.GPL
            );

            using var processor = new GhostscriptProcessor(gsVersion, true);

            var switches = new[]
            {
                "-q",
                "-dNOPAUSE",
                "-dBATCH",
                "-dSAFER",
                "-sDEVICE=pdfwrite",
                "-dCompatibilityLevel=1.4",
                $"-dPDFSETTINGS=/{pdfSetting}", // e.g. screen, ebook, printer
                $"-sOutputFile={tempOutputPath}",
                tempInputPath
            };

            processor.StartProcessing(switches, null);

            return File.ReadAllBytes(tempOutputPath);
        }
        finally
        {
            if (File.Exists(tempInputPath)) File.Delete(tempInputPath);
            if (File.Exists(tempOutputPath)) File.Delete(tempOutputPath);
        }
    }
})
.WithName("CompressPdf")
.DisableAntiforgery();

static byte[] CompressImage(byte[] imageBytes, int targetDPI)
{
    try
    {
        using var inputStream = new MemoryStream(imageBytes);
        using var image = SixLabors.ImageSharp.Image.Load(inputStream);
        using var outputStream = new MemoryStream();
        
        Console.WriteLine($"Original image: {image.Width}x{image.Height}");
        
        // Снижаем разрешение
        var scale = Math.Min(1.0, targetDPI / 300.0);
        if (scale < 1.0)
        {
            var newWidth = (int)(image.Width * scale);
            var newHeight = (int)(image.Height * scale);
            image.Mutate(x => x.Resize(newWidth, newHeight));
            Console.WriteLine($"Resized to: {newWidth}x{newHeight}");
        }
        
        // Сильное JPEG сжатие
        var encoder = new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder { Quality = 50 };
        image.Save(outputStream, encoder);
        
        return outputStream.ToArray();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error compressing image: {ex.Message}");
        return imageBytes;
    }
}


app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
