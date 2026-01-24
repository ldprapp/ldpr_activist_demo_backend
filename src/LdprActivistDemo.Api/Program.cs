using LdprActivistDemo.Application;
using LdprActivistDemo.Application.Otp;
using LdprActivistDemo.Persistence;

using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.Configure<OtpOptions>(builder.Configuration.GetSection("Otp"));
builder.Services.AddApplication();
builder.Services.AddPersistence(builder.Configuration);

var app = builder.Build();

var autoMigrate = builder.Configuration.GetValue<bool>("Database:AutoMigrate");
if(autoMigrate)
{
	using var scope = app.Services.CreateScope();
	var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
	await db.Database.MigrateAsync();
}

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();
app.Run();
