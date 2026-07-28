using System.ComponentModel.DataAnnotations;

namespace PortfolioApi.DTOs.Astrology;

/// <summary>Public request to receive the reading PDF by email.</summary>
public class RequestPdfDto
{
    [Required, EmailAddress, StringLength(160)] public string Email { get; set; } = string.Empty;
    [StringLength(120)] public string Name { get; set; } = string.Empty;
    [StringLength(20)] public string BirthDate { get; set; } = string.Empty;
    [StringLength(10)] public string BirthTime { get; set; } = string.Empty;
}

/// <summary>Admin view — decrypted PDF request.</summary>
public class PdfRequestView
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string BirthInfo { get; set; } = string.Empty;
    public string ApprovalStatus { get; set; } = "Pending";
    public string CreatedAt { get; set; } = string.Empty;
}
