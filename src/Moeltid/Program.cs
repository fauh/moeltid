using Microsoft.EntityFrameworkCore;
using Moeltid.Data;
using Moeltid.Endpoints;
using Moeltid.Services;
using Moeltid.Services.Attendances;
using Moeltid.Services.Email;
using Moeltid.Services.Events;
using Moeltid.Services.MealOptions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddAntiforgery();
builder.Services.AddHttpContextAccessor();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddSingleton<TokenGenerator>();
builder.Services.AddSingleton<SlugGenerator>();
builder.Services.AddScoped<IEventService, EventService>();
builder.Services.AddScoped<IMealOptionService, MealOptionService>();
builder.Services.AddScoped<IAttendanceService, AttendanceService>();
builder.Services.AddScoped<IEmailSender, ConsoleEmailSender>();

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

app.MapAttendanceEndpoints();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
