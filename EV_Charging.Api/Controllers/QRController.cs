/*
 * QRController.cs
 * IT22267504
 * Methmini, K. A. T.
 * 
 * This controller manages QR code generation and scanning for reservations
 */

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EV_Charging.Api.Services;
using EV_Charging.Api.DTOs;
using EV_Charging.Api.Utils;
using MongoDB.Driver;
using EV_Charging.Api.Models;
using EV_Charging.Api.Data;
using System.Text.Json;

namespace EV_Charging.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class QRController : ControllerBase
    {
        private readonly IQrCodeGenerator _qrCodeGenerator;
        private readonly IMongoCollection<Booking> _bookings;
        private readonly IMongoCollection<Station> _stations;
        private readonly IMongoCollection<EVOwner> _evOwners;
        private readonly ILogger<QRController> _logger;

        public QRController(
            IQrCodeGenerator qrCodeGenerator, 
            MongoDbContext context,
            ILogger<QRController> logger)
        {
            _qrCodeGenerator = qrCodeGenerator;
            _bookings = context.GetBookingsCollection();
            _stations = context.GetStationsCollection();
            _evOwners = context.EVOwners;
            _logger = logger;
        }

        // Generates QR code for a reservation with all booking details
        [HttpPost("generate/{bookingId}")]
        [Authorize(Roles = "EVOwner,Admin")]
        public async Task<ActionResult<ApiResponse<QRCodeResponseDto>>> GenerateQRCode(string bookingId)
        {
            try
            {
                var booking = await _bookings.Find(b => b.Id == bookingId).FirstOrDefaultAsync();
                
                if (booking == null)
                {
                    return NotFound(new ApiResponse<QRCodeResponseDto>
                    {
                        Success = false,
                        Message = "Booking not found"
                    });
                }

                // Get station details
                var station = await _stations.Find(s => s.Id == booking.StationId).FirstOrDefaultAsync();
                if (station == null)
                {
                    return NotFound(new ApiResponse<QRCodeResponseDto>
                    {
                        Success = false,
                        Message = "Station not found"
                    });
                }

                // Get EV Owner details
                var evOwner = await _evOwners.Find(e => e.Id == booking.UserId).FirstOrDefaultAsync();
                if (evOwner == null)
                {
                    return NotFound(new ApiResponse<QRCodeResponseDto>
                    {
                        Success = false,
                        Message = "EV Owner not found"
                    });
                }

                // Create QR payload with all reservation details
                var qrPayload = new QRCodePayload
                {
                    BookingId = booking.Id,
                    UserId = booking.UserId,
                    StationId = booking.StationId,
                    StationName = station.Name,
                    StationAddress = station.Location.Address,
                    OwnerName = evOwner.Name,
                    OwnerNIC = evOwner.NIC,
                    OwnerPhone = evOwner.Phone,
                    ReservationDateTime = booking.ReservationDateTime,
                    Status = booking.Status.ToString(),
                    CreatedAt = booking.CreatedAt,
                    GeneratedAt = DateTime.UtcNow
                };

                // Serialize payload to JSON
                var jsonPayload = JsonSerializer.Serialize(qrPayload);
                
                // Generate QR code
                var qrCode = _qrCodeGenerator.GenerateQrCode(jsonPayload);

                // Update booking with QR code
                var update = Builders<Booking>.Update
                    .Set(b => b.QRCode, qrCode)
                    .Set(b => b.UpdatedAt, DateTime.UtcNow);

                await _bookings.UpdateOneAsync(b => b.Id == bookingId, update);

                var response = new QRCodeResponseDto
                {
                    QRCode = qrCode,
                    BookingId = bookingId,
                    ReservationDetails = qrPayload,
                    Message = "QR code generated successfully"
                };

                return Ok(new ApiResponse<QRCodeResponseDto>
                {
                    Success = true,
                    Data = response,
                    Message = "QR code generated successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating QR code for booking {BookingId}", bookingId);
                return StatusCode(500, new ApiResponse<QRCodeResponseDto>
                {
                    Success = false,
                    Message = "An error occurred while generating QR code"
                });
            }
        }

        // Scans QR code and retrieves reservation details
        [HttpPost("scan")]
        [Authorize(Roles = "StationOperator,Admin")]
        public async Task<ActionResult<ApiResponse<BookingScanDto>>> ScanQRCode([FromBody] QRScanRequest scanRequest)
        {
            try
            {
                if (string.IsNullOrEmpty(scanRequest.QRPayload))
                {
                    return BadRequest(new ApiResponse<BookingScanDto>
                    {
                        Success = false,
                        Message = "QR payload is required"
                    });
                }

                // Extract and decode QR payload
                string jsonPayload;
                try
                {
                    // Remove QR_ prefix and decode base64
                    var encodedData = scanRequest.QRPayload.Replace("QR_", "");
                    var decodedBytes = Convert.FromBase64String(encodedData);
                    var fullPayload = System.Text.Encoding.UTF8.GetString(decodedBytes);
                    
                    // Extract JSON part (remove timestamp suffix)
                    var lastUnderscoreIndex = fullPayload.LastIndexOf('_');
                    jsonPayload = fullPayload.Substring(0, lastUnderscoreIndex);
                }
                catch (Exception)
                {
                    return BadRequest(new ApiResponse<BookingScanDto>
                    {
                        Success = false,
                        Message = "Invalid QR code format"
                    });
                }

                // Deserialize QR payload
                QRCodePayload qrData;
                try
                {
                    qrData = JsonSerializer.Deserialize<QRCodePayload>(jsonPayload);
                    if (qrData == null)
                    {
                        throw new JsonException("Deserialized payload is null");
                    }
                }
                catch (JsonException)
                {
                    return BadRequest(new ApiResponse<BookingScanDto>
                    {
                        Success = false,
                        Message = "Invalid QR code data format"
                    });
                }

                // Verify booking exists and get current status
                var booking = await _bookings.Find(b => b.Id == qrData.BookingId).FirstOrDefaultAsync();
                
                if (booking == null)
                {
                    return NotFound(new ApiResponse<BookingScanDto>
                    {
                        Success = false,
                        Message = "Booking not found"
                    });
                }

                // Validate QR code matches booking
                if (booking.QRCode != scanRequest.QRPayload)
                {
                    return BadRequest(new ApiResponse<BookingScanDto>
                    {
                        Success = false,
                        Message = "QR code does not match booking record"
                    });
                }

                // Check if booking is valid for scanning (not cancelled or completed)
                var isValid = booking.Status != BookingStatus.Cancelled && 
                             booking.Status != BookingStatus.Completed;

                var validationMessage = booking.Status switch
                {
                    BookingStatus.Cancelled => "Booking has been cancelled",
                    BookingStatus.Completed => "Booking has already been completed",
                    BookingStatus.Pending => "Booking is pending confirmation",
                    BookingStatus.Approved => "Booking is confirmed and ready",
                    BookingStatus.InProgress => "Booking session is currently in progress",
                    _ => "Booking status is valid"
                };

                var scanResult = new BookingScanDto
                {
                    BookingId = qrData.BookingId,
                    StationName = qrData.StationName,
                    StationAddress = qrData.StationAddress,
                    OwnerName = qrData.OwnerName,
                    OwnerNIC = qrData.OwnerNIC,
                    ReservationDateTime = qrData.ReservationDateTime,
                    Status = booking.Status.ToString(), // Use current status from DB
                    IsValid = isValid,
                    ValidationMessage = validationMessage
                };

                return Ok(new ApiResponse<BookingScanDto>
                {
                    Success = true,
                    Data = scanResult,
                    Message = "QR code scanned successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scanning QR code");
                return StatusCode(500, new ApiResponse<BookingScanDto>
                {
                    Success = false,
                    Message = "An error occurred while scanning QR code"
                });
            }
        }

        // Validates QR code and starts charging session (Operator only)
        [HttpPost("checkin")]
        [Authorize(Roles = "StationOperator")]
        public async Task<ActionResult<ApiResponse<object>>> CheckInWithQR([FromBody] QRScanRequest scanRequest)
        {
            try
            {
                // Extract booking ID from QR code directly
                string bookingId;
                try
                {
                    // Extract and decode QR payload to get booking ID
                    var encodedData = scanRequest.QRPayload.Replace("QR_", "");
                    var decodedBytes = Convert.FromBase64String(encodedData);
                    var fullPayload = System.Text.Encoding.UTF8.GetString(decodedBytes);
                    var lastUnderscoreIndex = fullPayload.LastIndexOf('_');
                    var jsonPayload = fullPayload.Substring(0, lastUnderscoreIndex);
                    var qrData = JsonSerializer.Deserialize<QRCodePayload>(jsonPayload);
                    bookingId = qrData?.BookingId ?? string.Empty;
                }
                catch (Exception)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Invalid QR code format"
                    });
                }
                var booking = await _bookings.Find(b => b.Id == bookingId).FirstOrDefaultAsync();

                if (booking == null || booking.Status != BookingStatus.Approved)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Booking is not ready for check-in"
                    });
                }

                // Update booking to start session
                var update = Builders<Booking>.Update
                    .Set(b => b.Status, BookingStatus.InProgress)
                    .Set(b => b.CheckInTime, DateTime.UtcNow)
                    .Set(b => b.UpdatedAt, DateTime.UtcNow);

                await _bookings.UpdateOneAsync(b => b.Id == bookingId, update);

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "Check-in successful. Charging session started."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during QR check-in");
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "An error occurred during check-in"
                });
            }
        }
    }

    // QR Code payload structure
    public class QRCodePayload
    {
        public string BookingId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string StationId { get; set; } = string.Empty;
        public string StationName { get; set; } = string.Empty;
        public string StationAddress { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public string OwnerNIC { get; set; } = string.Empty;
        public string OwnerPhone { get; set; } = string.Empty;
        public DateTime ReservationDateTime { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime GeneratedAt { get; set; }
    }

    // QR Code response DTO
    public class QRCodeResponseDto
    {
        public string QRCode { get; set; } = string.Empty;
        public string BookingId { get; set; } = string.Empty;
        public QRCodePayload ReservationDetails { get; set; } = new();
        public string Message { get; set; } = string.Empty;
    }
}