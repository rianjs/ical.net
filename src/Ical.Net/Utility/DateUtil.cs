using Ical.Net.DataTypes;
using NodaTime;
using NodaTime.TimeZones;

namespace Ical.Net.Utility;

internal static class DateUtil
{
    public static IDateTime StartOfDay(IDateTime dt)
        => dt.AddHours(-dt.Hour).AddMinutes(-dt.Minute).AddSeconds(-dt.Second);

    public static IDateTime EndOfDay(IDateTime dt)
        => StartOfDay(dt).AddDays(1).AddTicks(-1);

    public static DateTime GetSimpleDateTimeData(IDateTime dt)
        => DateTime.SpecifyKind(dt.Value, dt.IsUtc ? DateTimeKind.Utc : DateTimeKind.Local);

    public static DateTime SimpleDateTimeToMatch(IDateTime dt, IDateTime toMatch)
    {
        if (toMatch.IsUtc && dt.IsUtc)
        {
            return dt.Value;
        }
        if (toMatch.IsUtc)
        {
            return dt.Value.ToUniversalTime();
        }
        return dt.IsUtc ? dt.Value.ToLocalTime() : dt.Value;
    }

    public static IDateTime MatchTimeZone(IDateTime dt1, IDateTime dt2)
    {
        // Associate the date/time with the first.
        var copy = dt2;
        copy.AssociateWith(dt1);

        // If the dt1 time does not occur in the same time zone as the
        // dt2 time, then let's convert it so they can be used in the
        // same context (i.e. evaluation).
        if (dt1.TzId != null)
        {
            return string.Equals(dt1.TzId, copy.TzId, StringComparison.OrdinalIgnoreCase)
                ? copy
                : copy.ToTimeZone(dt1.TzId);
        }

        return dt1.IsUtc
            ? new CalDateTime(copy.AsUtc)
            : new CalDateTime(copy.AsSystemLocal);
    }

    public static DateTime AddWeeks(DateTime dt, int interval, DayOfWeek firstDayOfWeek)
    {
        // NOTE: fixes WeeklyUntilWkst2() eval.
        // NOTE: simplified the execution of this - fixes bug #3119920 - missing weekly occurences also
        dt = dt.AddDays(interval * 7);
        while (dt.DayOfWeek != firstDayOfWeek)
        {
            dt = dt.AddDays(-1);
        }

        return dt;
    }

    public static DateTime FirstDayOfWeek(DateTime dt, DayOfWeek firstDayOfWeek, out int offset)
    {
        offset = 0;
        while (dt.DayOfWeek != firstDayOfWeek)
        {
            dt = dt.AddDays(-1);
            offset++;
        }
        return dt;
    }

    private static readonly Lazy<Dictionary<string, string>> _windowsMapping
        = new(InitializeWindowsMappings, LazyThreadSafetyMode.PublicationOnly);

    private static Dictionary<string, string> InitializeWindowsMappings()
        => TzdbDateTimeZoneSource.Default.WindowsMapping.PrimaryMapping
            .ToDictionary(k => k.Key, v => v.Value, StringComparer.OrdinalIgnoreCase);

    public static readonly DateTimeZone LocalDateTimeZone
        = DateTimeZoneProviders.Tzdb.GetSystemDefault();

    /// <summary>
    /// Use this method to turn a raw string into a NodaTime DateTimeZone. It searches all time zone providers (IANA, BCL, serialization, etc) to see if
    /// the string matches. If it doesn't, it walks each provider, and checks to see if the time zone the provider knows about is contained within the
    /// target time zone string. Some older icalendar programs would generate nonstandard time zone strings, and this secondary check works around
    /// that.
    /// </summary>
    /// <param name="tzId">A BCL, IANA, or serialization time zone identifier</param>
    /// <param name="useLocalIfNotFound">If true, this method will return the system local time zone if tzId doesn't match a known time zone identifier.
    /// Otherwise, it will throw an exception.</param>
    public static DateTimeZone GetZone(string tzId, bool useLocalIfNotFound = true)
    {
        if (string.IsNullOrWhiteSpace(tzId))
        {
            return LocalDateTimeZone;
        }

        if (tzId.StartsWith("/", StringComparison.OrdinalIgnoreCase))
        {
            tzId = tzId.Substring(1, tzId.Length - 1);
        }

        var zone = DateTimeZoneProviders.Tzdb.GetZoneOrNull(tzId);
        if (zone != null)
        {
            return zone;
        }

        if (_windowsMapping.Value.TryGetValue(tzId, out var ianaZone))
        {
            return DateTimeZoneProviders.Tzdb.GetZoneOrNull(ianaZone);
        }

        zone = NodaTime.Xml.XmlSerializationSettings.DateTimeZoneProvider.GetZoneOrNull(tzId);
        if (zone != null)
        {
            return zone;
        }

        //US/Eastern is commonly represented as US-Eastern
        var newTzId = tzId.Replace("-", "/");
        zone = NodaTime.Xml.XmlSerializationSettings.DateTimeZoneProvider.GetZoneOrNull(newTzId);
        if (zone != null)
        {
            return zone;
        }

        foreach (var providerId in DateTimeZoneProviders.Tzdb.Ids.Where(tzId.Contains))
        {
            return DateTimeZoneProviders.Tzdb.GetZoneOrNull(providerId);
        }

        if (_windowsMapping.Value.Keys
                .Where(tzId.Contains)
                .Any(providerId => _windowsMapping.Value.TryGetValue(providerId, out ianaZone))
           )
        {
            return DateTimeZoneProviders.Tzdb.GetZoneOrNull(ianaZone);
        }

        foreach (var providerId in NodaTime.Xml.XmlSerializationSettings.DateTimeZoneProvider.Ids.Where(tzId.Contains))
        {
            return NodaTime.Xml.XmlSerializationSettings.DateTimeZoneProvider.GetZoneOrNull(providerId);
        }

        if (useLocalIfNotFound)
        {
            return LocalDateTimeZone;
        }

        throw new ArgumentException($"Unrecognized time zone id {tzId}");
    }

    public static DateTime ConvertToTimeZone(this DateTime dt, string sourceTz, string destTz)
    {
        var safe = DateTime.SpecifyKind(dt, DateTimeKind.Unspecified);
        var sourceTzi = TimeZoneInfo.FindSystemTimeZoneById(sourceTz);
        var destTzi = TimeZoneInfo.FindSystemTimeZoneById(destTz);
        return TimeZoneInfo.ConvertTime(safe, sourceTzi, destTzi);
    }

    public static DateTimeOffset ToDateTimeOffset(this DateTime dt, string sourceTz)
    {
        var safe = DateTime.SpecifyKind(dt, DateTimeKind.Unspecified);
        var sourceTzi = TimeZoneInfo.FindSystemTimeZoneById(sourceTz);
        // TODO: Allow the developer to choose which offset they're referring when "fall back" hours are repeated
        // var offset = sourceTzi.IsAmbiguousTime(dt)
        //     ? // ???
        //     : sourceTzi.GetUtcOffset(dt);

        var offset = sourceTzi.GetUtcOffset(dt);
        return new DateTimeOffset(safe, offset);
    }

    // TODO: Does the BCL support "serialization" time zones?
    // public static bool IsSerializationTimeZone(DateTimeZone zone)
    //     => NodaTime.Xml.XmlSerializationSettings.DateTimeZoneProvider.GetZoneOrNull(zone.Id) != null;

    /// <summary>
    /// Truncate to the specified TimeSpan's magnitude. For example, to truncate to the nearest second, use TimeSpan.FromSeconds(1)
    /// </summary>
    /// <param name="dateTime"></param>
    /// <param name="timeSpan"></param>
    /// <returns></returns>
    public static DateTime Truncate(this DateTime dateTime, TimeSpan timeSpan)
        => timeSpan == TimeSpan.Zero
            ? dateTime
            : dateTime.AddTicks(-(dateTime.Ticks % timeSpan.Ticks));

    public static int WeekOfMonth(DateTime d)
    {
        var isExact = d.Day % 7 == 0;
        var offset = isExact
            ? 0
            : 1;
        return (int) Math.Floor(d.Day / 7.0) + offset;
    }

    public static string GetIanaTimeZone(string tzId)
        => GetIanaName(TimeZoneInfo.FindSystemTimeZoneById(tzId));

    public static string GetIanaName(TimeZoneInfo tzi)
    {
        if (tzi.HasIanaId)
        {
            return tzi.Id;
        }

        return TimeZoneInfo.TryConvertWindowsIdToIanaId(tzi.StandardName, out var maybeIana)
            ? maybeIana
            : string.Empty;
    }

    public static string GetLocalSystemIanaTimeZone()
        => GetIanaName(TimeZoneInfo.Local);
}