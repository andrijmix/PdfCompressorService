public class PdfSettings
{
    public double MaxFileSizeInMB { get; set; }
    public double MinFileSizeInMB { get; set; }
    public string[] AllowedExtensions { get; set; } = Array.Empty<string>();
    public string CompressionLevel { get; set; } = "High";
}