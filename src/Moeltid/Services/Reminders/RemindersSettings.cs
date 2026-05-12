namespace Moeltid.Services.Reminders;

/// <summary>
/// Controls whether Hangfire and the reminder feature are active.
/// Set <c>Reminders:Enabled = false</c> in <c>appsettings.Production.json</c>
/// (or via env var <c>Reminders__Enabled=false</c>) to disable on hosting tiers
/// that don't keep a warm process (e.g. Render free tier).
/// </summary>
public sealed class RemindersSettings
{
    public const string SectionName = "Reminders";

    /// <summary>
    /// When false, Hangfire is not registered, <see cref="NullReminderService"/> is used,
    /// and the reminder UI is hidden on the manage page.
    /// Default <c>true</c> so development keeps working without any config change.
    /// </summary>
    public bool Enabled { get; init; } = true;
}
