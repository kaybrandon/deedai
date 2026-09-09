using System.Globalization;

namespace DeedAi.Api;

/// <summary>ISO week (Monday–Sunday) buckets for dashboard Volume Over Time.</summary>
public static class VolumeWeeks
{
    public const int MaxFilledWeeks = 104;

    public static DateTime StartOfIsoWeek(DateTime day)
    {
        var date = day.Date;
        var year = ISOWeek.GetYear(date);
        var week = ISOWeek.GetWeekOfYear(date);
        return ISOWeek.ToDateTime(year, week, DayOfWeek.Monday);
    }

    public static DateTime EndOfIsoWeek(DateTime weekStart) => weekStart.Date.AddDays(6);

    public static string FormatDay(DateTime day) =>
        day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
