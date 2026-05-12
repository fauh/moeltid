using Hangfire;
using Hangfire.Storage.SQLite;
using Microsoft.EntityFrameworkCore;
using Moeltid.Data;
using Moeltid.Endpoints;
using Moeltid.Services;
using Moeltid.Services.Attendances;
using Moeltid.Services.Email;
using Moeltid.Services.Events;
using Moeltid.Services.Invitees;
using Moeltid.Services.MealOptions;
using Moeltid.Services.Reminders;

var builder = WebApplication.CreateBuilder(args);

// ── Connection string — DATA_DIR env var for container deployments ────────────
// Development default: DATA_DIR not set → "Data Source=moeltid.db" (relative to CWD).
// Production (Fly.io): DATA_DIR=/data is set in fly.toml's [env] block so SQLite
// writes to the mounted persistent volume. Without it, SQLite tries to write to
// /app (root-owned) and the non-root container user gets permission denied.
// The env var is a short-hand; the full connection string can also be overridden
// directly via ConnectionStrings__DefaultConnection if preferred.
var dataDir = Environment.GetEnvironmentVariable("DATA_DIR");
if (!string.IsNullOrWhiteSpace(dataDir))
{
    builder.Configuration["ConnectionStrings:DefaultConnection"] =
        $"Data Source={Path.Combine(dataDir, "moeltid.db")}";
}

// ── Reminders / Hangfire — conditionally disabled in prod ────────────────────
builder.Services.Configure<RemindersSettings>(
    builder.Configuration.GetSection(RemindersSettings.SectionName));

var remindersSettings = builder.Configuration
    .GetSection(RemindersSettings.SectionName)
    .Get<RemindersSettings>() ?? new RemindersSettings();

// ── Core services ────────────────────────────────────────────────────────────
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddAntiforgery();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHealthChecks();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── Email ────────────────────────────────────────────────────────────────────
builder.Services.Configure<EmailSettings>(
    builder.Configuration.GetSection(EmailSettings.SectionName));

var emailSettings = builder.Configuration
    .GetSection(EmailSettings.SectionName)
    .Get<EmailSettings>() ?? new EmailSettings();

if (emailSettings.UseRealProvider)
{
    if (string.IsNullOrWhiteSpace(emailSettings.ApiKey))
        throw new InvalidOperationException(
            "EmailSettings:ApiKey must be set when EmailSettings:UseRealProvider is true. " +
            "In development, use `dotnet user-secrets set \"EmailSettings:ApiKey\" \"<key>\"`. " +
            "In production, set the host environment variable EmailSettings__ApiKey.");

    builder.Services.AddHttpClient<IEmailSender, ResendEmailSender>();
}
else
{
    builder.Services.AddScoped<IEmailSender, ConsoleEmailSender>();
}

// ── Hangfire (only when reminders are enabled) ───────────────────────────────
if (remindersSettings.Enabled)
{
    var hangfireConnStr = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Data Source=moeltid.db";
    builder.Services.AddHangfire(config =>
        config.UseSimpleAssemblyNameTypeSerializer()
              .UseRecommendedSerializerSettings()
              .UseSQLiteStorage(hangfireConnStr));
    builder.Services.AddHangfireServer();
    builder.Services.AddScoped<IReminderService, ReminderService>();
}
else
{
    // NullReminderService: all calls no-op (CancelAsync) or throw (ScheduleAsync,
    // which should never be called when the reminder UI is hidden).
    builder.Services.AddScoped<IReminderService, NullReminderService>();
}

// ── App services ─────────────────────────────────────────────────────────────
builder.Services.AddSingleton<TokenGenerator>();
builder.Services.AddSingleton<SlugGenerator>();
builder.Services.AddScoped<IEventService, EventService>();
builder.Services.AddScoped<IMealOptionService, MealOptionService>();
builder.Services.AddScoped<IAttendanceService, AttendanceService>();
builder.Services.AddScoped<IInviteeService, InviteeService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAntiforgery();

// Hangfire dashboard — Development only, and only when Hangfire is registered
if (app.Environment.IsDevelopment() && remindersSettings.Enabled)
{
    app.UseHangfireDashboard();
}

app.MapHealthChecks("/health");
app.MapAttendanceEndpoints();
app.MapExportEndpoints();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
