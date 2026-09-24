// SPDX-License-Identifier: MIT

using CosineKitty;
using WorkTrail.Application;

namespace WorkTrail.Services;

/// <summary>Places offline calendar observances in the same city-local celestial event timeline.</summary>
internal static class CelestialCalendarEventService
{
    internal static IReadOnlyList<CelestialAgendaEvent> Build(
        CelestialSnapshot snapshot, TimeZoneInfo zone,
        IReadOnlyList<CelestialCalendarHoliday> holidays,
        IReadOnlyList<CelestialCalendarSaint> saints,
        IReadOnlyList<string> selectedCountries, bool showSaints,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(zone);
        var selected = selectedCountries.ToHashSet(StringComparer.Ordinal);
        var endUtc = snapshot.InstantUtc.AddDays(2);
        var startDate = DateOnly.FromDateTime(snapshot.LocalTime.DateTime);
        var endDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(endUtc, zone).DateTime);
        var observer = new Observer(snapshot.Latitude, snapshot.Longitude, 0);
        var events = new List<CelestialAgendaEvent>();

        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var national = holidays.Where(holiday => holiday.Date == date && holiday.Kind == "holiday"
                && selected.Contains(holiday.Country)).ToArray();
            var dailySaints = showSaints
                ? saints.Where(saint => saint.Month == date.Month && saint.Day == date.Day).ToArray()
                : [];
            if (national.Length == 0 && dailySaints.Length == 0)
            {
                continue;
            }

            // Calendar entries are all-day observances; the event instant is only their visual sort anchor.
            // A polar day without sunrise uses local noon rather than fabricating a solar crossing.
            var anchor = SunriseOrNoon(date, observer, zone, cancellationToken);
            var offset = 0;
            foreach (var holiday in national)
            {
                var instant = anchor.AddSeconds(++offset);
                if (instant >= endUtc) continue;
                events.Add(new CelestialAgendaEvent(CelestialEventKind.Holiday, instant, null,
                    TimeZoneInfo.ConvertTime(instant, zone), null,
                    CalendarCountryCode: holiday.Country, CalendarLabel: holiday.Name,
                    CalendarQuality: holiday.Quality, CalendarDate: date));
            }

            foreach (var saint in dailySaints)
            {
                var instant = anchor.AddSeconds(++offset);
                if (instant >= endUtc) continue;
                events.Add(new CelestialAgendaEvent(CelestialEventKind.Saint, instant, null,
                    TimeZoneInfo.ConvertTime(instant, zone), null,
                    CalendarLabel: saint.NameLatin, CalendarEntryKey: saint.EventKey, CalendarDate: date));
            }
        }

        return events;
    }

    private static DateTimeOffset SunriseOrNoon(DateOnly date, Observer observer, TimeZoneInfo zone,
        CancellationToken cancellationToken)
    {
        var noon = TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(new TimeOnly(12, 0)), zone);
        var search = new AstroTime(noon.AddHours(-18));
        for (var index = 0; index < 3; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var rise = Astronomy.SearchRiseSet(Body.Sun, observer, Direction.Rise, search, 2.0);
            if (rise is null) break;
            var instant = new DateTimeOffset(rise.ToUtcDateTime());
            var localDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, zone).DateTime);
            if (localDate == date) return instant;
            if (localDate > date) break;
            search = rise.AddDays(1.0 / 86400);
        }

        return new DateTimeOffset(noon);
    }
}
