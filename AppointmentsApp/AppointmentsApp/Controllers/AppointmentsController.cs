using AppointmentsApp.DTOs;
using AppointmentsApp.Services;
using Microsoft.AspNetCore.Mvc;

namespace AppointmentsApp.Controllers;
[ApiController]
[Route("api/[controller]")]
public class AppointmentsController(IAppointmentService service) : ControllerBase
{
    // GET /api/appointments Zwraca listę wizyt z podstawowymi danymi pacjenta.
    [HttpGet]
    public async Task<IActionResult> GetAppointments([FromQuery] string? status, [FromQuery] string? patientLastName)
    {
        return Ok(await service.GetAppointments(status, patientLastName));
    }
    //GET /api/appointments/{idAppointment} Zwraca szczegóły jednej wizyty.

    [HttpGet("{id}")]
    public async Task<IActionResult> GetByIdAppointment([FromRoute] int id)
    {
        try
        {
            return Ok(await service.GetByIdAppointments(id));
        } catch  (Exception e) when (e.Message.StartsWith("[notfound]"))
        {
            return NotFound( new ErrorResponseDto{Message = e.Message});
        }
    }
    
    // // POST /api/appointments Dodaje nową wizytę.
    [HttpPost]
    public async Task<IActionResult> AddAppointment([FromBody] CreateAppointmentRequestDto dto)
    {
        try
        {
            var newAppointment = await service.AddAppointment(dto);
            return Created($"/api/appointments/{newAppointment.IdAppointment}", newAppointment);
        }
        catch (Exception e) when (e.Message.StartsWith("[conflict]"))
        {
            return Conflict(new ErrorResponseDto { Message = e.Message });
        }
        catch (Exception e) when (e.Message.StartsWith("[notfound]"))
        {
            return NotFound(new ErrorResponseDto { Message = e.Message });
        }
        catch (Exception e) when (e.Message.StartsWith("[badrequest]"))
        {
            return BadRequest(new ErrorResponseDto { Message = e.Message });
        }
    }
    
    // // PUT /api/appointments/{idAppointment} Aktualizuje istniejącą wizytę.
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateAppointment([FromRoute] int id, [FromBody] UpdateAppointmentRequestDto dto)
    {
        try
        {
            await service.UpdateAppointment(id, dto);
            return NoContent();
        }
        catch (Exception e) when (e.Message.StartsWith("[conflict]"))
        {
            return Conflict(new ErrorResponseDto { Message = e.Message });
        }
        catch (Exception e) when (e.Message.StartsWith("[notfound]"))
        {
            return NotFound(new ErrorResponseDto { Message = e.Message });
        }
        catch (Exception e) when (e.Message.StartsWith("[badrequest]"))
        {
            return BadRequest(new ErrorResponseDto { Message = e.Message });
        }
    }
    
    
    //DELETE /api/appointments/{idAppointment} Usuwa wizytę.
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAppointment([FromRoute] int id)
    {
        try
        {
            await service.DeleteAppointment(id);
            return NoContent();
        }
        catch (Exception e) when (e.Message.StartsWith("[notfound]"))
        {
            return NotFound(new ErrorResponseDto { Message = e.Message });
        }
        catch (Exception e) when (e.Message.StartsWith("[conflict]"))
        {
            return Conflict(new ErrorResponseDto { Message = e.Message });
        }
        
    }
}