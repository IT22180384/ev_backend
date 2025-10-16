/*
 * QrCodeGenerator.cs
 * IT22267504
 * Methmini, K. A. T.
 * 
 * Utility class for generating QR codes for reservations.
 * Provides interface and implementation for QR code generation.
 */

namespace EV_Charging.Api.Utils
{
    public interface IQrCodeGenerator
    {
        string GenerateQrCode(string data);
    }

    public class QrCodeGenerator : IQrCodeGenerator
    {
        public string GenerateQrCode(string data)
        {
            // Generate a QR code string for reservation data
            // This is a simple implementation that returns a base64-like string
            // In a real application, you would use a QR code library like QRCoder
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var encodedData = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{data}_{timestamp}"));
            return $"QR_{encodedData}";
        }
    }
}