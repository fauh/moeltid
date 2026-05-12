using Hangfire;
using Hangfire.Storage.SQLite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moeltid.Data;
using Moeltid.Endpoints;
using Moeltid.Services;
using Moeltid.Services.Attendances;
using Moeltid.Services.Email;
using Moeltid.Services.Events;
using Moeltid.Services.Invitees;
using Moeltid.Services.MealOptions;
using Moeltid.Services.MyEvents;
using Moeltid.Services.Reminders;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddAntiforgery();
builder.Services.AddHttpContextAccessor();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// EmailSettings — bind from config; fail-fast if UseRealProvider but ApiKey empty
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

// Hangfire — SQLite storage (same DB as app)
var hangfireConnStr = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=moeltid.db";
builder.Services.AddHangfire(config =>
    config.UseSimpleAssemblyNameTypeSerializer()
          .UseRecommendedSerializerSettings()
          .UseSQLiteStorage(hangfireConnStr));
builder.Services.AddHangfireServer();

builder.Services.AddSingleton<TokenGenerator>();
builder.Services.AddSingleton<SlugGenerator>();
builder.Services.AddScoped<IEventService, EventService>();
builder.Services.AddScoped<IMealOptionService, MealOptionService>();
builder.Services.AddScoped<IAttendanceService, AttendanceService>();
builder.Services.AddScoped<IInviteeService, InviteeService>();
builder.Services.AddScoped<IReminderService, ReminderService>();
builder.Services.AddScoped<IMyEventsService, MyEventsService>();

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

// Hangfire dashboard — Development only
if (app.Environment.IsDevelopment())
{
    app.UseHangfireDashboard();
}

app.MapAttendanceEndpoints();
app.MapExportEndpoints();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
