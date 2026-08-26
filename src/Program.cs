using GoRide.Api.Options;   
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

builder.Services.AddCors();

builder.Services
    .AddOptions<AsgardeoOptions>()
    .Bind(builder.Configuration.GetSection("Asgardeo"))
    .ValidateDataAnnotations()
    .Validate(o => !string.IsNullOrWhiteSpace(o.BaseUrl), "Asgardeo:BaseUrl is required")
    .Validate(o => !string.IsNullOrWhiteSpace(o.ClientId), "Asgardeo:ClientId is required")
    .Validate(o => !string.IsNullOrWhiteSpace(o.ClientSecret), "Asgardeo:ClientSecret is required")
    .ValidateOnStart();

builder.Services
    .AddOptions<AsgardeoMgmtOptions>()
    .Bind(builder.Configuration.GetSection("AsgardeoMgmt"))
    .ValidateOnStart();

builder.Services
    .AddOptions<AsgardeoRolesOptions>()
    .Bind(builder.Configuration.GetSection("AsgardeoRoles"))
    .ValidateOnStart();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
