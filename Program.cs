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

// Bind FastGPT options and HttpClient
builder.Services.Configure<ScoringApp.Config.FastGptOptions>(builder.Configuration.GetSection("FastGpt"));
builder.Services.Configure<ScoringApp.Config.FastGptCloudOptions>(builder.Configuration.GetSection("FastGptCloud"));
builder.Services.AddHttpClient<ScoringApp.Services.FastGptClient>();

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

// Simple request logging
app.Use(async (ctx, next) =>
{
	var sw = System.Diagnostics.Stopwatch.StartNew();
	var logger = ctx.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Request");
	logger.LogInformation("HTTP {Method} {Path}{Query} from {Remote}", ctx.Request.Method, ctx.Request.Path, ctx.Request.QueryString, ctx.Connection.RemoteIpAddress);
	await next();
	sw.Stop();
	logger.LogInformation("HTTP {Method} {Path} -> {StatusCode} in {Elapsed}ms", ctx.Request.Method, ctx.Request.Path, ctx.Response.StatusCode, sw.ElapsedMilliseconds);
});

app.UseAuthorization();

app.MapControllers();

// No-cache index.html to prevent stale UI via proxies/browsers
app.Use(async (ctx, next) =>
{
	if (ctx.Request.Path == "/" || (ctx.Request.Path.Value?.EndsWith("/index.html") ?? false))
	{
		ctx.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
		ctx.Response.Headers["Pragma"] = "no-cache";
		ctx.Response.Headers["Expires"] = "0";
	}
	await next();
});

app.UseDefaultFiles();
app.UseStaticFiles();

app.Run();
