using Microsoft.EntityFrameworkCore;
using Northwind.Recommendations.API.Data;
using Northwind.Recommendations.API.Services;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title       = "Northwind Recommendations API",
        Version     = "v1",
        Description = "Collaborative, Content-Based & Hybrid recommendations for Northwind"
    });
});
var connStr = builder.Configuration.GetConnectionString("Northwind")
    ?? throw new InvalidOperationException("Connection string 'Northwind' not found.");
builder.Services.AddDbContext<NorthwindDbContext>(opts =>
    opts.UseNpgsql(connStr)
        .EnableDetailedErrors()
        .EnableSensitiveDataLogging(builder.Environment.IsDevelopment()));
builder.Services.AddScoped<IRecommendationService, RecommendationService>();
builder.Services.AddCors(opts =>
    opts.AddPolicy("ReactApp", p =>
        p.WithOrigins(
            "http://localhost:5173", 
            "http://localhost:3000",
            "http://10.233.65.108:8081",
            "http://10.233.65.108:8082"
        )
         .AllowAnyHeader()
         .AllowAnyMethod()));
var app = builder.Build();
app.Urls.Add("http://0.0.0.0:5001");
app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Northwind Recs v1"));
app.UseCors("ReactApp");
app.UseAuthorization();
app.MapControllers();
app.Run();
