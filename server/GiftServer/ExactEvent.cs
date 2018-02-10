﻿using GiftServer.Exceptions;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Xml;

namespace GiftServer
{
    namespace Data
    {
        /// <summary>
        /// A rule engine for Exact Events
        /// </summary>
        /// <remarks>
        /// Exact Events are events that occur on an "exact" time scale. This does not mean that the time itself never varies; 
        /// indeed, with Daylight Savings Time and Leap Year, it undoubtedly will. Instead, Exact means that the event begins on it's Start Date and happens every Time Interval.
        /// 
        /// As an example, an Exact Event could be defined as starting on January 10th, 2017 and happening every month.In this case, the next event occurrences would be:
        /// - February 10th, 2017
        /// - March 10th, 2017
        /// - April 10th, 2017
        /// 
        /// This doesn't depend on the "week" it's in, or on a specific day of the week.
        /// </remarks>
        public class ExactEvent : RulesEngine, IEquatable<ExactEvent>
        {
            /// <summary>
            /// The ID for this Exact Event
            /// </summary>
            public override sealed ulong ID
            {
                get;
                private protected set;
            } = 0;

            private char timeInterval = '\0';
            /// <summary>
            /// The Time Interval for this event
            /// </summary>
            /// <remarks>
            /// This method will accept the following values:
            /// 
            /// - d, day, or daily
            /// - w, week, or weekly
            /// - m, month, or monthly
            /// - y, year, or yearly
            /// 
            /// In any case, but will return a single character.
            /// </remarks>
            public string TimeInterval
            {
                get
                {
                    return timeInterval.ToString();
                }
                set
                {
                    switch (value.ToLower())
                    {
                        case "d":
                        case "day":
                        case "daily":
                            timeInterval = 'D';
                            break;
                        case "w":
                        case "week":
                        case "weekly":
                            timeInterval = 'W';
                            break;
                        case "m":
                        case "month":
                        case "monthly":
                            timeInterval = 'M';
                            break;
                        case "y":
                        case "year":
                        case "yearly":
                            timeInterval = 'Y';
                            break;
                        default:
                            throw new ArgumentException(value);
                    }
                }
            }

            private int skipEvery = 0;
            /// <summary>
            /// Skip every x occurrences. Cannot be 0.
            /// </summary>
            public int SkipEvery
            {
                get
                {
                    return skipEvery;
                }
                set
                {
                    if (value > 0)
                    {
                        skipEvery = value;
                    }
                }
            }
            /// <summary>
            /// All occurrences of this event
            /// </summary>
            /// <remarks>
            /// This will return, in chronological order, event occurrences.
            /// 
            /// Please note - because of this, if there is no end date, _this iterator could iterate infinitely_. It is up to the caller to handle this.
            /// </remarks>
            public override IEnumerable<Occurrence> Occurrences
            {
                get
                {
                    DateTime currVal = Event.StartDate;
                    if (Event.EndDate.HasValue)
                    {
                        // We will have an end.
                        while (currVal <= Event.EndDate)
                        {
                            yield return new Occurrence(Event, currVal);
                            currVal = Increment(currVal);
                        }
                    }
                    else
                    {
                        // No end
                        while (true)
                        {
                            yield return new Occurrence(Event, currVal);
                            currVal = Increment(currVal);
                        }
                    }
                }
            }
            /// <summary>
            /// Fetch an existing ExactEvent from the database
            /// </summary>
            /// <param name="id">The ID for this exact event</param>
            /// <param name="e">The event this ruleset is tied to</param>
            public ExactEvent(ulong id, Event e)
            {
                Event = e;
                using (MySqlConnection con = new MySqlConnection(ConfigurationManager.ConnectionStrings["Development"].ConnectionString))
                {
                    con.Open();
                    using (MySqlCommand cmd = new MySqlCommand())
                    {
                        cmd.Connection = con;
                        cmd.CommandText = "SELECT ExactEventID, EventID, EventTimeInterval, EventSkipEvery FROM exact_events WHERE ExactEventID = @eid;";
                        cmd.Parameters.AddWithValue("@eid", id);
                        cmd.Prepare();
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                if (Event.ID != Convert.ToUInt64(reader["EventID"]))
                                {
                                    throw new InvalidOperationException("EventIDs do not match!");
                                }
                                ID = id;
                                TimeInterval = Convert.ToString(reader["EventTimeInterval"]);
                                SkipEvery = Convert.ToInt32(reader["EventSkipEvery"]);
                            }
                            else
                            {
                                throw new EventNotFoundException(id);
                            }
                        }
                    }
                }
            }
            /// <summary>
            /// Create a new ExactEvents Ruleset
            /// </summary>
            /// <param name="interval">The interval</param>
            /// <param name="skip">The SkipEvery</param>
            public ExactEvent(string interval, int skip)
            {
                TimeInterval = interval;
                SkipEvery = skip;
            }

            private DateTime Increment(DateTime currVal)
            {
                DateTime incremented;
                switch (timeInterval)
                {
                    case 'D':
                        // Increment by a day
                        incremented = currVal.AddDays(skipEvery);
                        break;
                    case 'W':
                        incremented = currVal.AddDays(7 * skipEvery);
                        break;
                    case 'M':
                        incremented = currVal.AddMonths(skipEvery);
                        break;
                    case 'Y':
                        incremented = currVal.AddYears(skipEvery);
                        break;
                    default:
                        incremented = currVal;
                        break;
                }
                // Ensure not in blackout days:
                if (Event.Blackouts.Exists(x => x.BlackoutDate.Year == incremented.Year &&
                                                x.BlackoutDate.Month == incremented.Month &&
                                                x.BlackoutDate.Day == incremented.Day))
                {
                    // The current date is a blackout!
                    return Increment(incremented);
                }
                else
                {
                    return incremented;
                }
            }

            /// <summary>
            /// Create a record of this ruleset in the database
            /// </summary>
            /// <returns>A status flag</returns>
            /// <remarks>
            /// Note that, since Event already creates this, it is unlikely the end user will need this method
            /// </remarks>
            public override void Create()
            {
                using (MySqlConnection con = new MySqlConnection(ConfigurationManager.ConnectionStrings["Development"].ConnectionString))
                {
                    con.Open();
                    using (MySqlCommand cmd = new MySqlCommand())
                    {
                        cmd.Connection = con;
                        cmd.CommandText = "INSERT INTO exact_events (EventID, EventTimeInterval, EventSkipEvery) "
                                        + "VALUES (@eid, @tin, @ski);";
                        cmd.Parameters.AddWithValue("@eid", Event.ID);
                        cmd.Parameters.AddWithValue("@tin", timeInterval);
                        cmd.Parameters.AddWithValue("@ski", skipEvery);
                        cmd.Prepare();
                        cmd.ExecuteNonQuery();
                        ID = Convert.ToUInt64(cmd.LastInsertedId);
                    }
                }
            }
            /// <summary>
            /// Update the record of this ruleset in the database
            /// </summary>
            /// <remarks>
            /// Note that, since Event already updates this, it is unlikely the end user will need this method
            /// </remarks>
            public override void Update()
            {
                if (ID == 0)
                {
                    Create();
                }
                else
                {
                    using (MySqlConnection con = new MySqlConnection(ConfigurationManager.ConnectionStrings["Development"].ConnectionString))
                    {
                        con.Open();
                        using (MySqlCommand cmd = new MySqlCommand())
                        {
                            cmd.Connection = con;
                            cmd.CommandText = "UPDATE exact_events SET EventTimeInterval = @tin, EventSkipEvery = @ski WHERE ExactEventID = @eid;";
                            cmd.Parameters.AddWithValue("@tin", timeInterval);
                            cmd.Parameters.AddWithValue("@ski", skipEvery);
                            cmd.Parameters.AddWithValue("@eid", ID);
                            cmd.Prepare();
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
            }
            /// <summary>
            /// Deletethe record of this ruleset in the database
            /// </summary>
            /// <remarks>
            /// Note that, since Event already deletes this, it is unlikely the end user will need this method
            /// </remarks>
            public override void Delete()
            {
                using (MySqlConnection con = new MySqlConnection(ConfigurationManager.ConnectionStrings["Development"].ConnectionString))
                {
                    con.Open();
                    using (MySqlCommand cmd = new MySqlCommand())
                    {
                        cmd.Connection = con;
                        cmd.CommandText = "DELETE FROM exact_events WHERE ExactEventID = @eid;";
                        cmd.Parameters.AddWithValue("@eid", ID);
                        cmd.Prepare();
                        cmd.ExecuteNonQuery();
                        ID = 0;
                    }
                }
            }

            /// <summary>
            /// See if these two objects are equal
            /// </summary>
            /// <param name="obj">The object to compare</param>
            /// <returns>Whether or not they are equal</returns>
            public override bool Equals(object obj)
            {
                if (obj != null && obj is ExactEvent e)
                {
                    return Equals(e);
                }
                else
                {
                    return false;
                }
            }
            /// <summary>
            /// See if these two RulesEngines are equivalent
            /// </summary>
            /// <param name="engine">The engine to compare</param>
            /// <returns>If they are the same</returns>
            public override bool Equals(RulesEngine engine)
            {
                if (engine != null && engine is ExactEvent e)
                {
                    return Equals(e);
                }
                else
                {
                    return false;
                }
            }
            /// <summary>
            /// See if these two ExactEvent Engines are the same
            /// </summary>
            /// <param name="engine">the ExactEvent engine to compare</param>
            /// <returns>If the two are equal</returns>
            public bool Equals(ExactEvent engine)
            {
                return engine != null && engine.ID == ID;
            }
            /// <summary>
            /// Get the hash code for this instnace
            /// </summary>
            /// <returns>The hash code</returns>
            public override int GetHashCode()
            {
                return ID.GetHashCode();
            }

            /// <summary>
            /// Serialize this ruleset
            /// </summary>
            /// <returns>A Serialized form of this ruleset</returns>
            /// <remarks>
            /// This XML Document has the following fields:
            /// - exactEventId: The ID for this rule set
            /// - timeInterval: A single character:
            ///     - 'D' -> Daily
            ///     - 'W' -> Weekly
            ///     - 'M' -> Monthly
            ///     - 'Y' -> Yearly
            /// - skipEvery: A number that represents how many iterations to skip (i.e., every = 1, every other = 2, ...)
            /// 
            /// This is all wrapped in an exactEvent container
            /// </remarks>
            public override XmlDocument Fetch()
            {
                XmlDocument info = new XmlDocument();
                XmlElement container = info.CreateElement("exactEvent");
                info.AppendChild(container);

                XmlElement id = info.CreateElement("exactEventId");
                id.InnerText = ID.ToString();
                XmlElement _timeInterval = info.CreateElement("timeInterval");
                _timeInterval.InnerText = timeInterval.ToString();
                XmlElement _skipEvery = info.CreateElement("skipEvery");
                _skipEvery.InnerText = skipEvery.ToString();

                container.AppendChild(id);
                container.AppendChild(_timeInterval);
                container.AppendChild(_skipEvery);
                return info;
            }
        }
    }
}
