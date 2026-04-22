using System.ComponentModel.DataAnnotations;

namespace AppointmentsApp.DTOs;

public class UpdateAppointmentRequestDto
{
    [Required]
    public int IdPatient { get; set; }
    [Required]
    public int IdDoctor { get; set; }
    public DateTime AppointmentDate { get; set; }
    [MaxLength(30), AllowedValues("Scheduled", "Completed", "Cancelled")]
    public string Status { get; set; } = string.Empty;
    [MaxLength(250)]
    public string Reason { get; set; } = string.Empty;
    [MaxLength(500)]
    public string? InternalNotes { get; set; }
}