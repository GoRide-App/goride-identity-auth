var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();



builder.Services.AddCors();

var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseCors(opt =>
{
    opt.AllowAnyHeader().AllowAnyMethod().WithOrigins("https://localhost:3000");
});


app.MapControllers();


app.Run();