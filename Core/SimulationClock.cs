using System;

namespace LivingSim.Core
{
    public enum Season
    {
        Spring,
        Summer,
        Autumn,
        Winter
    }

    public class SimulationClock
    {
        public long CurrentTick { get; private set; }

        // --- Time Configuration ---
        // 240 Ticks per Day. (24 seconds IRL at 100ms speed)
        public const int TicksPerHour = 10;
        public const int TicksPerDay = TicksPerHour * 24;

        // --- CALENDAR CONFIGURATION (120 Day Year) ---
        // 30 Days per Season. Long enough to feel the cold of Winter.
        public const int DaysPerSeason = 30;
        public const int SeasonsPerYear = 4;
        public const int DaysPerYear = DaysPerSeason * SeasonsPerYear;

        public SimulationClock()
        {
            CurrentTick = 0;
        }

        public void AdvanceTick()
        {
            CurrentTick++;
        }

        // --- Computed Properties ---
        public long CurrentDay => CurrentTick / TicksPerDay;
        public int Year => (int)(CurrentDay / DaysPerYear) + 1;
        public int DayOfYear => (int)(CurrentDay % DaysPerYear) + 1;
        public int Hour => (int)((CurrentTick % TicksPerDay) / TicksPerHour);

        public Season CurrentSeason
        {
            get
            {
                int seasonIndex = (int)((DayOfYear - 1) / DaysPerSeason);
                return (Season)(seasonIndex % 4);
            }
        }

        public bool IsNight => Hour >= 20 || Hour < 6;

        public string TimeString => $"{Hour:D2}:00";
    }
}
