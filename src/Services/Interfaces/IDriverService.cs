using Microsoft.AspNetCore.Mvc;
using SRC.Dtos;
using SRC.Entities;

namespace SRC.Services.Interfaces;

public interface IDriverService
{
    Task<List<DriverProfile>> GetDrivers(DriverProfileRequestDto requestDto);
}