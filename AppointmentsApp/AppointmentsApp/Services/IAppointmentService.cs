using AppointmentsApp.DTOs;

namespace AppointmentsApp.Services;

public interface IAppointmentService
{
    Task<IEnumerable<AppointmentListDto>> GetAppointments(string? status, string? patientLastName);
    Task<AppointmentDetailsDto?> GetByIdAppointments(int id);
    
    Task<AppointmentDetailsDto> AddAppointment(CreateAppointmentRequestDto dto);
    
    Task UpdateAppointment(int id, UpdateAppointmentRequestDto dto);
    
    Task DeleteAppointment(int id);
}