var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS (allow any origin or configure as needed)
builder.Services.AddCors(options =>
{
	options.AddDefaultPolicy(policy =>
	{
		policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
	});
});

// FastGPT HttpClient
builder.Services.AddHttpClient<ScoringApp.Services.FastGptClient>(client =>
{
	var baseUrl = Environment.GetEnvironmentVariable("FASTGPT_BASE_URL") ?? "http://fastgpt:3000/";
	client.BaseAddress = new Uri(baseUrl);
});

// Notifier and Worker
builder.Services.AddSingleton<ScoringApp.Services.ScoreNotifier>();
builder.Services.AddHostedService<ScoringApp.Services.ScoreWorker>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// app.UseHttpsRedirection(); // Disabled in container to allow plain HTTP on port 19466

app.UseCors();

app.UseAuthorization();

app.MapControllers();

app.Run();
