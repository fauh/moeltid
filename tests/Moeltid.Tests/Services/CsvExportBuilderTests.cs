using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using Moeltid.Models;
using Moeltid.Services.Exports;
using Shouldly;

namespace Moeltid.Tests.Services;

/// <summary>
/// Tests for CsvExportBuilder — pure helper, no DB needed.
/// </summary>
public class CsvExportBuilderTests
{
    // ── helpers ───────────────────────────────────────────────────────────────

    private static Event MakeEvent(string tzId = "UTC") => new()
    {
        Id = Guid.NewGuid(),
        Slug = "test-event-abc123",
        Title = "Team Lunch",
        StartsAt = new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero),
        Deadline = new DateTimeOffset(2026, 6, 14, 12, 0, 0, TimeSpan.Zero),
        TimeZoneId = tzId,
        OwnerName = "Alice",
        OwnerEmail = "alice@example.com",
        ManageToken = "secret-manage-token",
        CreatedAt = DateTimeOffset.UtcNow,
    };

    private static Attendance MakeFreeTextAttendance(
        Guid eventId,
        string name = "Bob",
        string? email = "bob@example.com",
        string freeText = "Veggie wrap",
        DateTimeOffset? submittedAt = null) => new()
    {
        Id = Guid.NewGuid(),
        EventId = eventId,
        Name = name,
        Email = email,
        EditToken = "secret-edit-token",
        OrderType = OrderType.FreeText,
        FreeTextOrder = freeText,
        SubmittedAt = submittedAt ?? new DateTimeOffset(2026, 6, 10, 11, 24, 0, TimeSpan.Zero),
    };

    private static Attendance MakePresetAttendance(
        Guid eventId,
        string name = "Carol",
        string? email = "carol@example.com",
        MealOption? option = null,
        DateTimeOffset? submittedAt = null)
    {
        option ??= new MealOption
        {
            Id = Guid.NewGuid(), EventId = eventId,
            Label = "Salmon", Tags = MealTag.Fish,
        };
        return new()
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            Name = name,
            Email = email,
            EditToken = "secret-edit-token-2",
            OrderType = OrderType.PresetOption,
            MealOptionId = option.Id,
            MealOption = option,
            SubmittedAt = submittedAt ?? new DateTimeOffset(2026, 6, 10, 13, 0, 0, TimeSpan.Zero),
        };
    }

    private static Invitee MakeInvitee(Guid eventId, string email) => new()
    {
        Id = Guid.NewGuid(),
        EventId = eventId,
        Email = email,
        InvitedAt = DateTimeOffset.UtcNow,
    };

    /// <summary>Parse the CSV bytes back into rows for easy assertion.</summary>
    private static List<dynamic> ParseCsv(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        // Skip BOM so CsvHelper reads cleanly
        using var sr = new StreamReader(ms, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        using var csv = new CsvReader(sr, new CsvConfiguration(CultureInfo.InvariantCulture));
        return csv.GetRecords<dynamic>().ToList();
    }

    // ── tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Build_BomPresentAtByteZero()
    {
        var ev = MakeEvent();
        var bytes = CsvExportBuilder.Build(ev, [], []);

        // UTF-8 BOM: 0xEF 0xBB 0xBF
        bytes[0].ShouldBe((byte)0xEF);
        bytes[1].ShouldBe((byte)0xBB);
        bytes[2].ShouldBe((byte)0xBF);
    }

    [Fact]
    public void Build_EmptyInput_ProducesHeaderRowOnly()
    {
        var ev = MakeEvent();
        var bytes = CsvExportBuilder.Build(ev, [], []);
        var rows = ParseCsv(bytes);

        rows.ShouldBeEmpty(); // header is present but no data rows
    }

    [Fact]
    public void Build_FreeTextRow_CorrectColumns()
    {
        var ev = MakeEvent();
        var attendance = MakeFreeTextAttendance(ev.Id);
        var bytes = CsvExportBuilder.Build(ev, [attendance], []);
        var rows = ParseCsv(bytes);

        rows.Count.ShouldBe(1);
        var row = rows[0] as IDictionary<string, object>;
        row!["Name"].ShouldBe("Bob");
        row["Email"].ShouldBe("bob@example.com");
        row["OrderType"].ShouldBe("FreeText");
        row["FreeTextOrder"].ShouldBe("Veggie wrap");
        row["OptionLabel"].ShouldBe(string.Empty);
        row["Tags"].ShouldBe(string.Empty);
    }

    [Fact]
    public void Build_PresetOptionRow_CorrectColumnsAndTags()
    {
        var ev = MakeEvent();
        var option = new MealOption
        {
            Id = Guid.NewGuid(), EventId = ev.Id,
            Label = "Salmon", Tags = MealTag.Fish,
        };
        var attendance = MakePresetAttendance(ev.Id, option: option);
        var bytes = CsvExportBuilder.Build(ev, [attendance], []);
        var rows = ParseCsv(bytes);

        var row = rows[0] as IDictionary<string, object>;
        row!["OrderType"].ShouldBe("PresetOption");
        row["OptionLabel"].ShouldBe("Salmon");
        row["Tags"].ShouldBe("Fish");
        row["FreeTextOrder"].ShouldBe(string.Empty);
    }

    [Fact]
    public void Build_MultiTagOption_SerializesAsReadableList()
    {
        var ev = MakeEvent();
        var option = new MealOption
        {
            Id = Guid.NewGuid(), EventId = ev.Id,
            Label = "Vegan pasta", Tags = MealTag.Vegetarian | MealTag.Vegan,
        };
        var attendance = MakePresetAttendance(ev.Id, option: option);
        var bytes = CsvExportBuilder.Build(ev, [attendance], []);
        var rows = ParseCsv(bytes);

        var row = rows[0] as IDictionary<string, object>;
        row!["Tags"].ShouldBe("Vegetarian, Vegan");
    }

    [Fact]
    public void Build_FreeTextWithCommasAndQuotes_RoundTripsCorrectly()
    {
        var ev = MakeEvent();
        var freeText = "Pasta, please — no \"nuts\", thanks";
        var attendance = MakeFreeTextAttendance(ev.Id, freeText: freeText);
        var bytes = CsvExportBuilder.Build(ev, [attendance], []);
        var rows = ParseCsv(bytes);

        var row = rows[0] as IDictionary<string, object>;
        row!["FreeTextOrder"].ShouldBe(freeText);
    }

    [Fact]
    public void Build_AnonymousAttendee_EmptyEmailColumn()
    {
        var ev = MakeEvent();
        var attendance = MakeFreeTextAttendance(ev.Id, name: "Anonymous", email: null);
        var bytes = CsvExportBuilder.Build(ev, [attendance], []);
        var rows = ParseCsv(bytes);

        var row = rows[0] as IDictionary<string, object>;
        row!["Name"].ShouldBe("Anonymous");
        row["Email"].ShouldBe(string.Empty);
    }

    [Fact]
    public void Build_NoOrderYetRow_ForUnorderedInvitee()
    {
        var ev = MakeEvent();
        var invitee = MakeInvitee(ev.Id, "pending@example.com");
        var bytes = CsvExportBuilder.Build(ev, [], [invitee]);
        var rows = ParseCsv(bytes);

        rows.Count.ShouldBe(1);
        var row = rows[0] as IDictionary<string, object>;
        row!["OrderType"].ShouldBe("NoOrderYet");
        row["Email"].ShouldBe("pending@example.com");
        row["SubmittedAt_OwnerTZ"].ShouldBe(string.Empty);
        row["SubmittedAt_UTC"].ShouldBe(string.Empty);
    }

    [Fact]
    public void Build_InviteeWhoOrdered_NotDoubledAsNoOrderYet()
    {
        var ev = MakeEvent();
        var attendance = MakeFreeTextAttendance(ev.Id, email: "ordered@example.com");
        var invitee = MakeInvitee(ev.Id, "ordered@example.com"); // same email
        var bytes = CsvExportBuilder.Build(ev, [attendance], [invitee]);
        var rows = ParseCsv(bytes);

        // Only the attendance row; invitee should be suppressed
        rows.Count.ShouldBe(1);
        var row = rows[0] as IDictionary<string, object>;
        row!["OrderType"].ShouldBe("FreeText");
    }

    [Fact]
    public void Build_TokensNotPresentInOutput()
    {
        var ev = MakeEvent(); // ManageToken = "secret-manage-token"
        var attendance = MakeFreeTextAttendance(ev.Id); // EditToken = "secret-edit-token"
        var invitee = MakeInvitee(ev.Id, "inv@example.com");
        var bytes = CsvExportBuilder.Build(ev, [attendance], [invitee]);
        var text = Encoding.UTF8.GetString(bytes);

        text.ShouldNotContain("secret-manage-token");
        text.ShouldNotContain("secret-edit-token");
    }

    [Fact]
    public void Build_SubmittedAt_RenderedInOwnerTimezone()
    {
        // Stockholm is UTC+2 in summer — submission at 11:24 UTC → 13:24 local
        var ev = MakeEvent(tzId: "Europe/Stockholm");
        var submittedAtUtc = new DateTimeOffset(2026, 6, 10, 11, 24, 0, TimeSpan.Zero);
        var attendance = MakeFreeTextAttendance(ev.Id, submittedAt: submittedAtUtc);
        var bytes = CsvExportBuilder.Build(ev, [attendance], []);
        var rows = ParseCsv(bytes);

        var row = rows[0] as IDictionary<string, object>;
        row!["SubmittedAt_OwnerTZ"].ShouldBe("2026-06-10 13:24 Europe/Stockholm");
        row["SubmittedAt_UTC"].ShouldBe("2026-06-10 11:24Z");
    }
}
