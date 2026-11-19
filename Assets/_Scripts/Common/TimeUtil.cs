using System;

public static class TimeUtil
{
    public static double HoursSince(string isoTime)
    {
        if (DateTime.TryParse(isoTime, out DateTime past))
        {
            return (DateTime.UtcNow - past).TotalHours;
        }
        return 0;
    }
}
