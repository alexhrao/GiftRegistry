﻿using System;

namespace GiftServer
{
    namespace Data
    {
        public class EventFuture
        {
            public ulong EventFutureId
            {
                get;
                private set;
            } = 0;
            public readonly int Day;
            public readonly int Month;
            public readonly int Year;
            public EventFuture(ulong id, int Year, int Month, int Day)
            {
                this.EventFutureId = id;
                this.Year = Year;
                this.Month = Month;
                this.Day = Day;
            }
            
            public EventFuture(int Year, int Month, int Day)
            {
                this.Year = Year;
                this.Month = Month;
                this.Day = Day;
            }
        }
    }
}