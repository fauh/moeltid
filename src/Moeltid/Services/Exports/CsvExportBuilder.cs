using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using Moeltid.Models;

namespace Moeltid.Services.Exports;

/// <summary>
/// Pure helper — no DB, no I/O.
/// Takes an Event + its attendances (MealOption must be included) + invitees,
/// returns a UTF-8-with-BOM CSV as a byte array ready to stream.
/// </summary>
public static class CsvExportBuilder
{
    /// <summary>Column order matches the locked spec in phase-6-plan.md §Decisions.</summary>
    public sealed record ExportRow(
        string Name,
        string Email,
        string OrderType,
        string OptionLabel,
        string FreeTextOrder,
        string Tags,
        string SubmittedAt_OwnerTZ,
        string SubmittedAt_UTC);

    private static readonly CsvConfiguration CsvConfig = new(CultureInfo.InvariantCulture)
    {
        // Write the header row
        HasHeaderRecord = true,
    };

    public static byte[] Build(
        Event ev,
        IReadOnlyList<Attendance> attendances,
        IReadOnlyList<Invitee> invitees)
    {
        var rows = BuildRows(ev, attendances, invitees);

        // UTF-8 with BOM so Excel opens cleanly on Windows
        using var ms = new MemoryStream();
        using var sw = new StreamWriter(ms, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        using var csv = new CsvWriter(sw, CsvConfig);

        csv.WriteRecords(rows);
        sw.Flush();
        return ms.ToArray();
    }

    private static IEnumerable<ExportRow> BuildRows(
        Event ev,
        IReadOnlyList<Attendance> attendances,
        IReadOnlyList<Invitee> invitees)
    {
        // Track which invitee emails have an attendance so we can emit NoOrderYet for the rest
        var orderedEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var tz = SafeGetTz(ev.TimeZoneId);

        foreach (var a in attendances)
        {
            orderedEmails.Add(a.Email ?? string.Empty);

            var (orderType, optionLabel, freeText, tags) = a.OrderType switch
            {
                Models.OrderType.PresetOption => (
                    "PresetOption",
                    a.MealOption?.Label ?? string.Empty,
                    string.Empty,
                    SerializeTags(a.MealOption?.Tags ?? MealTag.None)),
                Models.OrderType.FreeText => (
                    "FreeText",
                    string.Empty,
                    a.FreeTextOrder ?? string.Empty,
                    string.Empty),
                _ => ("Unknown", string.Empty, string.Empty, string.Empty),
            };

            var localDt = TimeZoneInfo.ConvertTime(a.SubmittedAt, tz);
            var ownerTz = $"{localDt:yyyy-MM-dd HH:mm} {ev.TimeZoneId}";
            var utcStr = $"{a.SubmittedAt:yyyy-MM-dd HH:mm}Z";

            yield return new ExportRow(
                Name: a.Name,
                Email: a.Email ?? string.Empty,
                OrderType: orderType,
                OptionLabel: optionLabel,
                FreeTextOrder: freeText,
                Tags: tags,
                SubmittedAt_OwnerTZ: ownerTz,
                SubmittedAt_UTC: utcStr);
        }

        // NoOrderYet rows for invitees who haven't ordered
        foreach (var inv in invitees)
        {
            if (orderedEmails.Contains(inv.Email)) continue;

            yield return new ExportRow(
                Name: inv.Email,   // no name available for unfulfilled invitees
                Email: inv.Email,
                OrderType: "NoOrderYet",
                OptionLabel: string.Empty,
                FreeTextOrder: string.Empty,
                Tags: string.Empty,
                SubmittedAt_OwnerTZ: string.Empty,
                SubmittedAt_UTC: string.Empty);
        }
    }

    private static string SerializeTags(MealTag tags)
    {
        if (tags == MealTag.None) return string.Empty;

        var parts = new List<string>();
        foreach (MealTag flag in Enum.GetValues<MealTag>())
        {
            if (flag == MealTag.None) continue;
            if (tags.HasFlag(flag)) parts.Add(flag.ToString());
        }
        return string.Join(", ", parts);
    }

    private static TimeZoneInfo SafeGetTz(string ianaId)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(ianaId); }
        catch { return TimeZoneInfo.Utc; }
    }
}
