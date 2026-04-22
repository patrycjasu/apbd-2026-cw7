using System.Text;
using AppointmentsApp.DTOs;
using Microsoft.Data.SqlClient;

namespace AppointmentsApp.Services;

public class AppointmentService(IConfiguration configuration) : IAppointmentService
{
    public async Task<IEnumerable<AppointmentListDto>> GetAppointments(string? status, string? patientLastName)
    {
        var result = new List<AppointmentListDto>();

        var sqlCommand = new StringBuilder("""
                                           SELECT
                                               a.IdAppointment,
                                               a.AppointmentDate,
                                               a.Status,
                                               a.Reason,
                                               p.FirstName + N' ' + p.LastName AS PatientFullName,
                                               p.Email AS PatientEmail
                                           FROM dbo.Appointments a
                                           JOIN dbo.Patients p ON p.IdPatient = a.IdPatient
                                           WHERE (@Status IS NULL OR a.Status = @Status)
                                             AND (@PatientLastName IS NULL OR p.LastName = @PatientLastName);
                                           """);
        
        
        await using var connection = new SqlConnection(configuration.GetConnectionString("DefaultConnection"));
        await using var command = new SqlCommand(sqlCommand.ToString(), connection);

        if (status is not null)
        {
            command.Parameters.AddWithValue("@Status", status);
        }
        else
        {
            command.Parameters.AddWithValue("@Status", DBNull.Value);
        }

        if (patientLastName is not null)
        {
            command.Parameters.AddWithValue("@PatientLastName", patientLastName);
        }
        else
        {
            command.Parameters.AddWithValue("@PatientLastName", DBNull.Value);
        }
        
        await connection.OpenAsync();
        
        var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(new()
            {
                IdAppointment = reader.GetInt32(reader.GetOrdinal("IdAppointment")),
                AppointmentDate = reader.GetDateTime(reader.GetOrdinal("AppointmentDate")),
                Status = reader.GetString(reader.GetOrdinal("Status")),
                Reason = reader.GetString(reader.GetOrdinal("Reason")),
                PatientFullName = reader.GetString(reader.GetOrdinal("PatientFullName")),
                PatientEmail = reader.GetString(reader.GetOrdinal("PatientEmail")),
            });
        }
        
        return result;
    }

    public async Task<AppointmentDetailsDto?> GetByIdAppointments(int id)
    {
        AppointmentDetailsDto? result = null;
        
        var sqlCommand = new StringBuilder("""
                                           SELECT a.IdAppointment,
                                                  a.AppointmentDate,
                                                  a.Status,
                                                  a.Reason,
                                                  p.FirstName + N' ' + p.LastName AS PatientFullName,
                                                  p.Email AS PatientEmail,
                                                  p.PhoneNumber AS PatientPhoneNumber,
                                                  d.LicenseNumber AS DoctorLicenseNumber,
                                                  a.InternalNotes,
                                                  a.CreatedAt
                                           FROM dbo.Appointments a
                                           JOIN dbo.Patients p ON p.IdPatient = a.IdPatient
                                           JOIN dbo.Doctors d ON d.IdDoctor = a.IdDoctor
                                           WHERE IdAppointment = @IdAppointment;
                                           """);
        
        await using var connection = new SqlConnection(configuration.GetConnectionString("DefaultConnection"));
        await using var command = new SqlCommand(sqlCommand.ToString(), connection);
        
        command.Parameters.AddWithValue("@IdAppointment", id);
        
        await connection.OpenAsync();
        
        var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result ??= new()
            
            {
                IdAppointment = reader.GetInt32(reader.GetOrdinal("IdAppointment")),
                AppointmentDate = reader.GetDateTime(reader.GetOrdinal("AppointmentDate")),
                Status = reader.GetString(reader.GetOrdinal("Status")),
                Reason = reader.GetString(reader.GetOrdinal("Reason")),
                PatientFullName = reader.GetString(reader.GetOrdinal("PatientFullName")),
                PatientEmail = reader.GetString(reader.GetOrdinal("PatientEmail")),
                PatientPhoneNumber = reader.GetString(reader.GetOrdinal("PatientPhoneNumber")),
                DoctorLicenseNumber = reader.GetString(reader.GetOrdinal("DoctorLicenseNumber")),
                InternalNotes = reader.IsDBNull(reader.GetOrdinal("InternalNotes")) ? null : reader.GetString(reader.GetOrdinal("InternalNotes")),
                CreatedAt =  reader.GetDateTime(reader.GetOrdinal("CreatedAt"))
            };
            
        }
        
        if (result is not null) return result;
        throw new Exception("[notfound] Appointment not found");
    }

    public async Task<AppointmentDetailsDto> AddAppointment(CreateAppointmentRequestDto dto)
    {
        await using var connection = new SqlConnection(configuration.GetConnectionString("DefaultConnection"));
        await using var command = new SqlCommand();
        
        await connection.OpenAsync();
        
        await using var transaction = connection.BeginTransaction();
        command.Transaction = transaction;
        command.Connection = connection;


        try
        {
            //czy aktywny pacjent i czy istnieje
            command.CommandText = "select Patients.IsActive from Patients where patients.IdPatient = @IdPatient;";
            command.Parameters.AddWithValue("@IdPatient", dto.IdPatient);
            var isActivePatient = await command.ExecuteScalarAsync();
            if (isActivePatient is null)
            {
                throw new Exception("[notfound] Patient not found");
            }

            if (!(bool)isActivePatient)
            {
                throw new Exception("[conflict] Patient is not active");
            }

            command.Parameters.Clear();

            //czy aktywny doktor i czy istnieje
            command.CommandText = "select Doctors.IsActive from Doctors where doctors.IdDoctor = @IdDoctor;";
            command.Parameters.AddWithValue("@IdDoctor", dto.IdDoctor);
            var isActiveDoctor = await command.ExecuteScalarAsync();
            if (isActiveDoctor is null)
            {
                throw new Exception("[notfound] Doctor not found");
            }

            if (!(bool)isActiveDoctor)
            {
                throw new Exception("[conflict] Doctor is not active");
            }
            //czy termin wizyty nie jest w przeszlosci
            if (dto.AppointmentDate < DateTime.Today)
            {
                throw new  Exception("[badrequest] Appointment date cannot be in the past");
            }
            //czy lekarz nie ma innej wizyty wtedy
            command.Parameters.Clear();
            command.CommandText =
                "select Appointments.AppointmentDate from Appointments join Doctors on Doctors.IdDoctor = Appointments.IdDoctor where doctors.idDoctor = @IdDoctor and appointments.AppointmentDate = @AppointmentDate;";
            command.Parameters.AddWithValue("@IdDoctor", dto.IdDoctor);
            command.Parameters.AddWithValue("@AppointmentDate", dto.AppointmentDate);
            
            var exists =  await command.ExecuteScalarAsync();
            if (exists is not null)
            {
                throw new  Exception("[conflict] Doctor is already booked on this date");
            }
            
            command.Parameters.Clear();
            command.CommandText = """"
                                  insert into appointments(IdPatient, IdDoctor, AppointmentDate, Status, Reason)
                                  output inserted.IdAppointment
                                  values (@IdPatient, @IdDoctor, @AppointmentDate, 'Scheduled', @Reason);
                                  """";
            command.Parameters.AddWithValue("@IdDoctor", dto.IdDoctor);
            command.Parameters.AddWithValue("@IdPatient", dto.IdPatient);
            command.Parameters.AddWithValue("@AppointmentDate", dto.AppointmentDate);
            command.Parameters.AddWithValue("@Reason", dto.Reason);
            
            var newId = Convert.ToInt32(await command.ExecuteScalarAsync());
            await transaction.CommitAsync();

            return await GetByIdAppointments(newId);

        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            throw;
        }
        
    }

    public async Task UpdateAppointment(int id, UpdateAppointmentRequestDto dto)
    {
        
        await using var connection = new SqlConnection(configuration.GetConnectionString("DefaultConnection"));
        await using var command = new SqlCommand();
        command.Connection = connection;
        await command.Connection.OpenAsync();
        
        //czy aktywny pacjent i czy istnieje
        command.CommandText = "select Patients.IsActive from Patients where patients.IdPatient = @IdPatient;";
        command.Parameters.AddWithValue("@IdPatient", dto.IdPatient);
        var isActivePatient = await command.ExecuteScalarAsync();
        if (isActivePatient is null)
        {
            throw new Exception("[notfound] Patient not found");
        }

        if (!(bool)isActivePatient)
        {
            throw new Exception("[conflict] Patient is not active");
        }

        command.Parameters.Clear();

        //czy aktywny doktor i czy istnieje
        command.CommandText = "select Doctors.IsActive from Doctors where doctors.IdDoctor = @IdDoctor;";
        command.Parameters.AddWithValue("@IdDoctor", dto.IdDoctor);
        var isActiveDoctor = await command.ExecuteScalarAsync();
        if (isActiveDoctor is null)
        {
            throw new Exception("[notfound] Doctor not found");
        }

        if (!(bool)isActiveDoctor)
        {
            throw new Exception("[conflict] Doctor is not active");
        }

        command.Parameters.Clear();
        //czy istnieje wizyta
        
        command.CommandText = "select Status, AppointmentDate from appointments where IdAppointment = @IdAppointment;";
        command.Parameters.AddWithValue("@IdAppointment", id);
        
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            throw new Exception("[notfound] Appointment not found");
        }
        
        var currStatus =  reader.GetString(reader.GetOrdinal("Status"));
        var currDate =  reader.GetDateTime(reader.GetOrdinal("AppointmentDate"));
        
        await reader.CloseAsync();
        
        if (currStatus is null)
        {
            throw new Exception("[notfound] Appointment not found");
        }

        if (currStatus.ToString().Equals("Completed") && currDate != dto.AppointmentDate )
        {
            throw new Exception("[conflict] Appointment is completed, cannot change date");
        }
        
        command.Parameters.Clear();
        command.CommandText =
            "select Appointments.AppointmentDate from Appointments join Doctors on Doctors.IdDoctor = Appointments.IdDoctor where doctors.idDoctor = @IdDoctor and appointments.AppointmentDate = @AppointmentDate;";
        command.Parameters.AddWithValue("@IdDoctor", dto.IdDoctor);
        command.Parameters.AddWithValue("@AppointmentDate", dto.AppointmentDate);
            
        var exists =  await command.ExecuteScalarAsync();
        if (exists is not null)
        {
            throw new  Exception("[conflict] Doctor is already booked on this date");
        }
        
        command.Parameters.Clear();
        command.CommandText = """
                              update appointments
                              set idPatient = @IdPatient,
                                  idDoctor = @IdDoctor,
                                  AppointmentDate = @AppointmentDate,
                                  Status = @Status,
                                  Reason = @Reason,
                                  InternalNotes = @InternalNotes
                              where  IdAppointment = @IdAppointment;
                              """;
        command.Parameters.AddWithValue("@IdDoctor", dto.IdDoctor);
        command.Parameters.AddWithValue("@IdPatient", dto.IdPatient);
        command.Parameters.AddWithValue("@AppointmentDate", dto.AppointmentDate);
        command.Parameters.AddWithValue("@Status", dto.Status);
        command.Parameters.AddWithValue("@Reason", dto.Reason);
        command.Parameters.AddWithValue("@IdAppointment", id);
        command.Parameters.AddWithValue("@InternalNotes", (object?) dto.InternalNotes ?? DBNull.Value);
        
        await command.ExecuteNonQueryAsync();

    }
    
    public async Task DeleteAppointment(int id)
    {
        await using var connection = new SqlConnection(configuration.GetConnectionString("DefaultConnection"));
        await using var command = new SqlCommand();
        
        command.Connection = connection;
        await command.Connection.OpenAsync();
        
        command.CommandText = "select Status from Appointments where IdAppointment = @IdAppointment;";
        command.Parameters.AddWithValue("@IdAppointment", id);
        
        var status = await command.ExecuteScalarAsync();
        if (status is null)
        {
            throw new Exception("[notfound] Appointment not found");
        } 
        if (status.ToString().Equals("Completed")) throw new Exception("[conflict] Appointment is completed");
        command.Parameters.Clear();
        
        await using var transaction = connection.BeginTransaction();
        command.Transaction = transaction;

        try
        {
            
            command.CommandText = "delete from Appointments where IdAppointment = @IdAppointment;";
            command.Parameters.AddWithValue("@IdAppointment", id);
            
            await command.ExecuteNonQueryAsync();
            await transaction.CommitAsync();

        }
        catch (Exception e)
        {
            await transaction.RollbackAsync();
            throw;
        }
        
    }
}