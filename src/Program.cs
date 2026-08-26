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

app.UseCors(opt =>
{
    opt.AllowAnyHeader().AllowAnyMethod().WithOrigins("https://localhost:3000");
});


app.MapControllers();


app.Run();