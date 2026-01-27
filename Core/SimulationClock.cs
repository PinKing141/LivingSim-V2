namespace LivingSim.Core
{
    /// <summary>
    /// Represents the four seasons of the year.
    /// </summary>
    public enum Season { Spring, Summer, Autumn, Winter }

    /// <summary>
    /// A simulation clock that tracks time in discrete ticks.
    /// Tracks time in ticks and exposes derived time units.
    /// </summary>
    public sealed class SimulationClock 
    {
        /// <summary>
        //---- Constants ----
        public const int TicksPerHour = 25; // Reduced from 100 to make days pass faster
        public const int HoursPerDay = 24;
        public const int DaysPerSeason = 91;
        public const int DaysPerYear = DaysPerSeason * 4 + 1; // 365
        public const int TicksPerDay = TicksPerHour * HoursPerDay;
        public const int TicksPerYear = TicksPerDay * DaysPerYear;

        // --- State Properties ---
        public long CurrentTick {get; private set; }

        //--- Derived Time Properties ---
        public int CurrentHour => (int)((CurrentTick % TicksPerDay) / TicksPerHour);
        public int CurrentDay => (int)((CurrentTick / TicksPerDay) % DaysPerYear);
        public bool IsNight => CurrentHour >= 18 || CurrentHour < 6; // Night is from 6 PM to 5:59 AM
        public int CurrentYear => (int)(CurrentTick / TicksPerYear);
        public Season CurrentSeason
        {
            get
            {
                int dayOfYear = CurrentDay;
                if (dayOfYear < DaysPerSeason) return Season.Spring;
                if (dayOfYear < DaysPerSeason * 2) return Season.Summer;
                if (dayOfYear < DaysPerSeason * 3) return Season.Autumn;
                return Season.Winter;
            }
        }
        
        // ---Constructors---
        public SimulationClock(long startingTick = 0)
        {
            CurrentTick = startingTick;
        }

        // ---Public API---
        public void AdvanceTick()
        {
            CurrentTick ++;
        }

        public void AdvanceTicks(long ticks)
        {
            if (ticks < 0)
            {
                throw new System.ArgumentException("Cannot advance negative ticks.");
            }
            CurrentTick += ticks;
        }
        public bool IsNewDay(long previousTick)
        {
            return (previousTick / TicksPerDay) != (CurrentTick / TicksPerDay);
        }

        public bool IsNewYear(long previousTick)
        {
            return (previousTick / TicksPerYear) != (CurrentTick / TicksPerYear);
        }
    }
}
