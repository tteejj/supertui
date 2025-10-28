using System;
using System.Globalization;
using System.Text.RegularExpressions;
using SuperTUI.Core.Interfaces;

namespace SuperTUI.Core.Services
{
    /// <summary>
    /// Smart input parser for natural language dates and durations
    /// Singleton service for consistent parsing across the application
    /// </summary>
    public class SmartInputParser : ISmartInputParser
    {
        private static SmartInputParser instance;
        public static SmartInputParser Instance => instance ??= new SmartInputParser();

        private SmartInputParser()
        {
        }

        #region Date Parsing

        /// <summary>
        /// Parse date from natural language input
        /// </summary>
        public DateTime? ParseDate(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return null;

            input = input.Trim().ToLowerInvariant();

            // Try standard DateTime.Parse first
            if (DateTime.TryParse(input, out var standardDate))
                return standardDate.Date;

            // Short format: YYYYMMDD (20251030)
            if (Regex.IsMatch(input, @"^\d{8}$"))
            {
                if (DateTime.TryParseExact(input, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var shortDate))
                    return shortDate.Date;
            }

            // Compact format: YYMMDD (251030)
            if (Regex.IsMatch(input, @"^\d{6}$"))
            {
                if (DateTime.TryParseExact(input, "yyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var compactDate))
                    return compactDate.Date;
            }

            // Relative dates with keywords
            switch (input)
            {
                case "today":
                case "now":
                    return DateTime.Today;

                case "tomorrow":
                case "tmr":
                case "tom":
                    return DateTime.Today.AddDays(1);

                case "yesterday":
                case "yday":
                    return DateTime.Today.AddDays(-1);

                case "next week":
                case "nextweek":
                    return DateTime.Today.AddDays(7);

                case "last week":
                case "lastweek":
                    return DateTime.Today.AddDays(-7);

                case "next month":
                case "nextmonth":
                    return DateTime.Today.AddMonths(1);

                case "last month":
                case "lastmonth":
                    return DateTime.Today.AddMonths(-1);

                case "next year":
                case "nextyear":
                    return DateTime.Today.AddYears(1);

                case "monday":
                case "mon":
                    return GetNextWeekday(DayOfWeek.Monday);

                case "tuesday":
                case "tue":
                case "tues":
                    return GetNextWeekday(DayOfWeek.Tuesday);

                case "wednesday":
                case "wed":
                    return GetNextWeekday(DayOfWeek.Wednesday);

                case "thursday":
                case "thu":
                case "thur":
                case "thurs":
                    return GetNextWeekday(DayOfWeek.Thursday);

                case "friday":
                case "fri":
                    return GetNextWeekday(DayOfWeek.Friday);

                case "saturday":
                case "sat":
                    return GetNextWeekday(DayOfWeek.Saturday);

                case "sunday":
                case "sun":
                    return GetNextWeekday(DayOfWeek.Sunday);
            }

            // Relative offset: +N or -N (days)
            var offsetMatch = Regex.Match(input, @"^([+-])(\d+)([dwmy])?$");
            if (offsetMatch.Success)
            {
                var sign = offsetMatch.Groups[1].Value == "+" ? 1 : -1;
                var amount = int.Parse(offsetMatch.Groups[2].Value) * sign;
                var unit = offsetMatch.Groups[3].Success ? offsetMatch.Groups[3].Value : "d";

                return unit switch
                {
                    "d" => DateTime.Today.AddDays(amount),
                    "w" => DateTime.Today.AddDays(amount * 7),
                    "m" => DateTime.Today.AddMonths(amount),
                    "y" => DateTime.Today.AddYears(amount),
                    _ => null
                };
            }

            // "in N days/weeks/months"
            var inMatch = Regex.Match(input, @"^in\s+(\d+)\s+(day|week|month|year)s?$");
            if (inMatch.Success)
            {
                var amount = int.Parse(inMatch.Groups[1].Value);
                var unit = inMatch.Groups[2].Value;

                return unit switch
                {
                    "day" => DateTime.Today.AddDays(amount),
                    "week" => DateTime.Today.AddDays(amount * 7),
                    "month" => DateTime.Today.AddMonths(amount),
                    "year" => DateTime.Today.AddYears(amount),
                    _ => null
                };
            }

            // "N days/weeks/months ago"
            var agoMatch = Regex.Match(input, @"^(\d+)\s+(day|week|month|year)s?\s+ago$");
            if (agoMatch.Success)
            {
                var amount = int.Parse(agoMatch.Groups[1].Value);
                var unit = agoMatch.Groups[2].Value;

                return unit switch
                {
                    "day" => DateTime.Today.AddDays(-amount),
                    "week" => DateTime.Today.AddDays(-amount * 7),
                    "month" => DateTime.Today.AddMonths(-amount),
                    "year" => DateTime.Today.AddYears(-amount),
                    _ => null
                };
            }

            return null;
        }

        /// <summary>
        /// Try parse date with out parameter
        /// </summary>
        public bool TryParseDate(string input, out DateTime result)
        {
            var parsed = ParseDate(input);
            if (parsed.HasValue)
            {
                result = parsed.Value;
                return true;
            }

            result = default;
            return false;
        }

        /// <summary>
        /// Get next occurrence of a weekday
        /// </summary>
        private DateTime GetNextWeekday(DayOfWeek targetDay)
        {
            var today = DateTime.Today;
            var daysUntilTarget = ((int)targetDay - (int)today.DayOfWeek + 7) % 7;

            // If today is the target day, return next week's occurrence
            if (daysUntilTarget == 0)
                daysUntilTarget = 7;

            return today.AddDays(daysUntilTarget);
        }

        #endregion

        #region Duration Parsing

        /// <summary>
        /// Parse duration from input
        /// </summary>
        public TimeSpan? ParseDuration(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return null;

            input = input.Trim().ToLowerInvariant();

            // Try standard TimeSpan.Parse first
            if (TimeSpan.TryParse(input, out var standardDuration))
                return standardDuration;

            // Hours: "2h", "2.5h", "2hr", "2 hours"
            var hourMatch = Regex.Match(input, @"^(\d+\.?\d*)\s*h(?:r|rs?|ours?)?$");
            if (hourMatch.Success)
            {
                if (double.TryParse(hourMatch.Groups[1].Value, out var hours))
                    return TimeSpan.FromHours(hours);
            }

            // Minutes: "30m", "30min", "30 minutes"
            var minMatch = Regex.Match(input, @"^(\d+)\s*m(?:in|ins?|inutes?)?$");
            if (minMatch.Success)
            {
                if (int.TryParse(minMatch.Groups[1].Value, out var minutes))
                    return TimeSpan.FromMinutes(minutes);
            }

            // Days: "2d", "2 days"
            var dayMatch = Regex.Match(input, @"^(\d+)\s*d(?:ay|ays)?$");
            if (dayMatch.Success)
            {
                if (int.TryParse(dayMatch.Groups[1].Value, out var days))
                    return TimeSpan.FromDays(days);
            }

            // Seconds: "30s", "30sec", "30 seconds"
            var secMatch = Regex.Match(input, @"^(\d+)\s*s(?:ec|ecs?|econds?)?$");
            if (secMatch.Success)
            {
                if (int.TryParse(secMatch.Groups[1].Value, out var seconds))
                    return TimeSpan.FromSeconds(seconds);
            }

            // Weeks: "2w", "2 weeks"
            var weekMatch = Regex.Match(input, @"^(\d+)\s*w(?:k|ks?|eeks?)?$");
            if (weekMatch.Success)
            {
                if (int.TryParse(weekMatch.Groups[1].Value, out var weeks))
                    return TimeSpan.FromDays(weeks * 7);
            }

            // Combined format: "2h30m", "1h 30m", "2d 3h 15m"
            var combinedMatch = Regex.Match(input, @"^(?:(\d+)\s*d)?[,\s]*(?:(\d+)\s*h)?[,\s]*(?:(\d+)\s*m)?[,\s]*(?:(\d+)\s*s)?$");
            if (combinedMatch.Success &&
                (combinedMatch.Groups[1].Success || combinedMatch.Groups[2].Success ||
                 combinedMatch.Groups[3].Success || combinedMatch.Groups[4].Success))
            {
                var days = combinedMatch.Groups[1].Success ? int.Parse(combinedMatch.Groups[1].Value) : 0;
                var hours = combinedMatch.Groups[2].Success ? int.Parse(combinedMatch.Groups[2].Value) : 0;
                var minutes = combinedMatch.Groups[3].Success ? int.Parse(combinedMatch.Groups[3].Value) : 0;
                var seconds = combinedMatch.Groups[4].Success ? int.Parse(combinedMatch.Groups[4].Value) : 0;

                return new TimeSpan(days, hours, minutes, seconds);
            }

            // Decimal hours without unit: "2.5" → 2.5 hours
            if (double.TryParse(input, out var decimalHours) && decimalHours >= 0 && decimalHours <= 24)
            {
                return TimeSpan.FromHours(decimalHours);
            }

            return null;
        }

        /// <summary>
        /// Try parse duration with out parameter
        /// </summary>
        public bool TryParseDuration(string input, out TimeSpan result)
        {
            var parsed = ParseDuration(input);
            if (parsed.HasValue)
            {
                result = parsed.Value;
                return true;
            }

            result = default;
            return false;
        }

        #endregion
    }
}
