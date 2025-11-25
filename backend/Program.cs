using backend.Config;
using backend.Infrastructure;
using backend.Services;
using Microsoft.Extensions.Caching.Memory;

var builder = WebApplication.CreateBuilder(args);

// Configuration
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("Email"));
builder.Services.Configure<WhatsAppOptions>(builder.Configuration.GetSection("WhatsApp"));

// Repository
builder.Services.AddSingleton<MongoRepo>();
builder.Services.AddMemoryCache();

// Add services to the container.
builder.Services.AddSingleton<WebhookService>();
builder.Services.AddSingleton<GeminiService>();
builder.Services.AddSingleton<WhatsAppService>();
builder.Services.AddSingleton<IEmailService, EmailService>();
builder.Services.AddSingleton<IConversationStore>(_ => new MemoryConversationStore(
                _.GetRequiredService<IMemoryCache>(),
                TimeSpan.FromDays(1)));

builder.Services.AddControllers();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHttpClient("meta", client => {
    client.BaseAddress = new Uri("https://graph.facebook.com/");
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
