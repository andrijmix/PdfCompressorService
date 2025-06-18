public class PdfSettings
{
    public double MaxFileSizeInMB { get; set; }
    public double MinFileSizeInMB { get; set; }
    public double TargetDPI { get; set; } = 150; // Default DPI for compression
    public string[] AllowedExtensions { get; set; } = Array.Empty<string>();
    public string CompressionLevel { get; set; } = "High";
}