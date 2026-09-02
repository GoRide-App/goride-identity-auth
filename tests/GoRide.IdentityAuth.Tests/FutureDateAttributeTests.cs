using System.ComponentModel.DataAnnotations;
using SRC.Dtos;
using SRC.Validation;

namespace GoRide.IdentityAuth.Tests;

public class FutureDateAttributeTests
{
    private static readonly FutureDateAttribute Sut = new();

    [Fact]
    public void Tomorrow_IsValid_Today_And_Yesterday_AreNot()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        Assert.True(Sut.IsValid(today.AddDays(1)));
        Assert.False(Sut.IsValid(today));
        Assert.False(Sut.IsValid(today.AddDays(-1)));
    }

    [Fact]
    public void Null_IsValid_SoOptionalFieldsCanBeOmitted()
    {
        Assert.True(Sut.IsValid(null));
    }

    [Fact]
    public void ExpiredLicence_FailsDriverProfileValidation_WithAFieldMessage()
    {
        var dto = new CreateDriverProfileRequestDto
        {
            VehicleMake = "Toyota",
            VehicleModel = "Aqua",
            VehiclePlate = "CAB-1234",
            VehicleTypeCode = "car",
            LicenseNumber = "B1234567",
            LicenseExpiry = new DateOnly(2020, 1, 1)
        };

        var results = new List<ValidationResult>();
        var valid = Validator.TryValidateObject(dto, new ValidationContext(dto), results, validateAllProperties: true);

        Assert.False(valid);
        var failure = Assert.Single(results);
        Assert.Contains(nameof(dto.LicenseExpiry), failure.MemberNames);
        Assert.Contains("future", failure.ErrorMessage);
    }

    [Fact]
    public void ShortPlateAndLicence_FailValidation()
    {
        var dto = new UpdateVehicleDto
        {
            VehicleMake = "T",
            VehicleModel = "Aqua",
            VehiclePlate = "AB",
            VehicleTypeCode = "car",
            LicenseNumber = "123",
            LicenseExpiry = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(1)
        };

        var results = new List<ValidationResult>();
        Validator.TryValidateObject(dto, new ValidationContext(dto), results, validateAllProperties: true);

        var members = results.SelectMany(r => r.MemberNames).ToHashSet();
        Assert.Equal(new HashSet<string> { "VehicleMake", "VehiclePlate", "LicenseNumber" }, members);
    }
}
