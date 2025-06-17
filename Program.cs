using iTextSharp.text.pdf;
using Microsoft.Extensions.Options;

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
    // Validate the file size and type
    var pdfSettings = settings.Value;
    if (file == null || file.Length == 0)
        return Results.BadRequest("No file uploaded.");

    // Check if the file size exceeds the maximum allowed size
    var maxSizeInBytes = pdfSettings.MaxFileSizeInMB * 1024 * 1024;
    if (file.Length > maxSizeInBytes)
        return Results.BadRequest($"File size exceeds the maximum limit of {pdfSettings.MaxFileSizeInMB} MB.");
    // Check if the file size is below the minimum allowed size
    var minSizeInBytes = pdfSettings.MinFileSizeInMB * 1024 * 1024;
    if (file.Length < minSizeInBytes)
        return Results.BadRequest($"File size is below the minimum limit of {pdfSettings.MinFileSizeInMB} MB.");
    // Check if the file type is allowed
    var allowedExtensions = pdfSettings.AllowedExtensions;
    var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
    if (!allowedExtensions.Contains(fileExtension))
        return Results.BadRequest($"File type '{fileExtension}' is not allowed. Allowed types: {string.Join(", ", allowedExtensions)}.");
        

    if (file.ContentType != "application/pdf")
        return Results.BadRequest("Only PDF files are allowed.");
    try
    {
        using var inputStream = file.OpenReadStream();
        using var outputStream = new MemoryStream();

        using (var reader = new iTextSharp.text.pdf.PdfReader(inputStream))
        using (var stamper = new iTextSharp.text.pdf.PdfStamper(reader, outputStream))
        {
            stamper.SetFullCompression();

            for (int i = 1; i <= reader.NumberOfPages; i++)
            {
                reader.RemoveUnusedObjects();
            }
        }
        var compressedBytes = outputStream.ToArray(); 
        return Results.File(compressedBytes, "application/pdf",$"compressed_{file.FileName}");
    }
    catch (Exception ex)
        {
        return Results.Problem($"An error occurred while compressing the PDF: {ex.Message}");
    }

    //return Results.Ok(new
    //{
    //    Message = "PDF file recieved, compression will be impremeted next",
    //    FileName =file.FileName,
    //    Size = file.Length,
    //    ContentType = file.ContentType
    //});
})
    .WithName("CompressPdf")
    .DisableAntiforgery();

app.MapPost("/upload", async (IFormFile file) =>
{
    if (file == null || file.Length == 0)
        return Results.BadRequest("No file uploaded.");
    //REading the file content
    using var stream = file.OpenReadStream();
    using var memoryStream = new MemoryStream();
    await stream.CopyToAsync(memoryStream);

    var fileBytes = memoryStream.ToArray();

    return Results.Ok(new
    {
        FileName = file.FileName,
        Size = file.Length,
        ContentType = file.ContentType,
        FirstBytes = Convert.ToHexString(fileBytes.Take(10).ToArray())
    });

})

.WithName("getFileUpload")
.DisableAntiforgery();

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
