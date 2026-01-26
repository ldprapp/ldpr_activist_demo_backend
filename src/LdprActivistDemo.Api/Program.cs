using LdprActivistDemo.Api.Middleware;
using LdprActivistDemo.Application;
using LdprActivistDemo.Application.Otp;
using LdprActivistDemo.Persistence;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
	.AddJsonOptions(o =>
	{
		o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
	});

builder.Services.Configure<ApiBehaviorOptions>(o => o.SuppressModelStateInvalidFilter = true);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.Configure<OtpOptions>(builder.Configuration.GetSection("Otp"));
builder.Services.AddApplication();
builder.Services.AddPersistence(builder.Configuration);

var app = builder.Build();

await using(var scope = app.Services.CreateAsyncScope())
{
	var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
	var autoMigrate = builder.Configuration.GetValue<bool>("Database:AutoMigrate");

	if(autoMigrate)
	{
		await db.Database.MigrateAsync(app.Lifetime.ApplicationStopping);
	}

	var geoSeeder = scope.ServiceProvider.GetRequiredService<GeoDbSeeder>();
	await geoSeeder.SeedAsync(app.Lifetime.ApplicationStopping);
}

app.UseMiddleware<ApiExceptionHandlingMiddleware>();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();
app.Run();
