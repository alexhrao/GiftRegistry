﻿using GiftServer.Exceptions;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Xml;

namespace GiftServer
{
    namespace Data
    {
        /// <summary>
        /// An event that can recur
        /// </summary>
        public class Event : ISynchronizable, IFetchable, IEquatable<Event>, IComparable<Event>
        {
            /// <summary>
            /// The EventID for this Event
            /// </summary>
            public ulong EventId
            {
                get;
                private set;
            } = 0;
            /// <summary>
            /// The Name for this event
            /// </summary>
            public string Name = "";
            /// <summary>
            /// The Starting Date of this event
            /// </summary>
            public DateTime StartDate;
            /// <summary>
            /// The Ending date of this event.
            /// </summary>
            /// <remarks>
            /// If this is null, then there is no end date
            /// </remarks>
            public DateTime? EndDate;
            /// <summary>
            /// The owner of this event
            /// </summary>
            public User User;
            /// <summary>
            /// The Rules Engine
            /// </summary>
            /// <remarks>
            /// The Rules Engine is a set of rules that describe when the next occurrence of an event will be.
            /// </remarks>
            public RulesEngine Rules;
            /// <summary>
            /// A list of Blackout Dates.
            /// </summary>
            public List<Blackout> Blackouts = new List<Blackout>();
            /// <summary>
            /// A list of groups this event is visible to
            /// </summary>
            public List<Group> Groups
            {
                get
                {
                    List<Group> groups = new List<Group>();
                    using (MySqlConnection con = new MySqlConnection(ConfigurationManager.ConnectionStrings["Development"].ConnectionString))
                    {
                        con.Open();
                        using (MySqlCommand cmd = new MySqlCommand())
                        {
                            cmd.Connection = con;
                            cmd.CommandText = "SELECT GroupID FROM groups_events WHERE EventID = @eid;";
                            cmd.Parameters.AddWithValue("@eid", EventId);
                            cmd.Prepare();
                            using (MySqlDataReader reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    groups.Add(new Group(Convert.ToUInt64(reader["GroupID"])));
                                }
                                return groups;
                            }
                        }
                    }
                }
            }
            /// <summary>
            /// Find out if this object is equal to this event
            /// </summary>
            /// <param name="obj">The object to compare</param>
            /// <returns>A boolean of whether they are equal or not</returns>
            public override bool Equals(object obj)
            {
                if (obj != null && obj is Event e)
                {
                    return Equals(e);
                }
                else
                {
                    return false;
                }
            }
            /// <summary>
            /// Check if this Event is equal to another event
            /// </summary>
            /// <param name="evnt">The event to compare</param>
            /// <returns>False if null or not the same event</returns>
            public bool Equals(Event evnt)
            {
                return evnt != null && evnt.EventId == EventId;
            }
            /// <summary>
            /// 
            /// </summary>
            /// <returns></returns>
            public override int GetHashCode()
            {
                return EventId.GetHashCode();
            }
            /// <summary>
            /// Compares two events
            /// </summary>
            /// <param name="e">The event to compare</param>
            /// <returns>-1 if this event is before, 0 if this event is on the same day, and 1 if this event is after</returns>
            public int CompareTo(Event e)
            {
                return GetNearestOccurrence().CompareTo(e.GetNearestOccurrence());
            }
            /// <summary>
            /// Fetch an existing Event from the database
            /// </summary>
            /// <param name="id">The EventID</param>
            public Event(ulong id)
            {
                using (MySqlConnection con = new MySqlConnection(ConfigurationManager.ConnectionStrings["Development"].ConnectionString))
                {
                    con.Open();
                    using (MySqlCommand cmd = new MySqlCommand())
                    {
                        cmd.Connection = con;
                        cmd.CommandText = "SELECT EventID, EventName, EventStartDate, EventEndDate, UserID FROM user_events WHERE EventID = @eid;";
                        cmd.Parameters.AddWithValue("@eid", id);
                        cmd.Prepare();
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                EventId = id;
                                Name = Convert.ToString(reader["EventName"]);
                                StartDate = (DateTime)(reader["EventStartDate"]);
                                EndDate = (DateTime?)(reader["EventEndDate"]);
                                User = new User(Convert.ToUInt64(reader["UserID"]));
                            }
                            else
                            {
                                // Throw new exception
                                throw new EventNotFoundException(id);
                            }
                        }
                    }
                    // Get rule engine
                    using (MySqlCommand cmd = new MySqlCommand())
                    {
                        cmd.Connection = con;
                        cmd.CommandText = "SELECT ExactEventID FROM exact_events WHERE EventID = @eid;";
                        cmd.Parameters.AddWithValue("@eid", id);
                        cmd.Prepare();
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                Rules = new ExactEvent(Convert.ToUInt64(reader["ExactEventID"]));
                            }
                        }
                    }
                    if (Rules == null)
                    {
                        using (MySqlCommand cmd = new MySqlCommand())
                        {
                            cmd.Connection = con;
                            cmd.CommandText = "SELECT RelativeEventID FROM relative_events WHERE EventID = @eid;";
                            cmd.Parameters.AddWithValue("@eid", id);
                            cmd.Prepare();
                            using (MySqlDataReader reader = cmd.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    Rules = new RelativeEvent(Convert.ToUInt64(reader["RelativeEventID"]));
                                }
                            }
                        }
                    }
                    using (MySqlCommand cmd = new MySqlCommand())
                    {
                        cmd.Connection = con;
                        cmd.CommandText = "SELECT BlackoutEventID FROM blackout_events WHERE EventID = @eid;";
                        cmd.Parameters.AddWithValue("@eid", id);
                        cmd.Prepare();
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                Blackouts.Add(new Blackout(Convert.ToUInt64(reader["BlackoutEventID"])));
                            }
                        }
                    }
                }
            }
            /// <summary>
            /// Get the closest occurrence that happens in the future
            /// </summary>
            /// <param name="near">The date to find an event near</param>
            /// <returns>The occurrence for this event - or null, if no occurrence after near is found</returns>
            public Occurrence GetNearestOccurrence(DateTime near)
            {
                foreach (Occurrence occur in Rules.Occurrences)
                {
                    if (occur.Date >= near)
                    {
                        return occur;
                    }
                }
                return null;
            }
            /// <summary>
            /// Get the closest occurrence that happens in the future from today
            /// </summary>
            /// <returns>The same as GetNearestOccurrence(DateTime near), where near is Today</returns>
            public Occurrence GetNearestOccurrence()
            {
                return GetNearestOccurrence(DateTime.Today);
            }

            /// <summary>
            /// Give the events in order
            /// </summary>
            /// <remarks>
            /// Given a pool of events, this will continue to return the next event.
            /// 
            /// Note that this means, if event A occurs monthly, and event B occurs yearly, event A could show up many times before event B
            /// </remarks>
            /// <param name="events">The pool of events</param>
            /// <param name="start">The starting date</param>
            /// <param name="stop">The ending date</param>
            /// <returns></returns>
            public static IEnumerable<Occurrence> PoolOrder(List<Event> events, DateTime start, DateTime stop)
            {
                while (start <= stop)
                {
                    events = events.OrderBy(e => e.GetNearestOccurrence(start)).ToList();
                    start = events[0].GetNearestOccurrence(start).Date;
                    foreach (Event e in events)
                    {
                        // As long as event occurrence date does not change keep chugging
                        if (start.Equals(e.GetNearestOccurrence(start)))
                        {
                            yield return new Occurrence(e, start);
                        }
                        else
                        {
                            start = e.GetNearestOccurrence(start).Date;
                            break;
                        }
                    }
                }
                // Events are sorted by their first future date - so for each of them, iterate until the date changes.
                // Change start date to that day, reorder the list, and engage
                // Reorder list 
            }
            /// <summary>
            /// Create a record of this event in the database
            /// </summary>
            /// <returns>A status flag</returns>
            /// <remarks>
            /// This also creates the necessary rules engine.
            /// </remarks>
            public bool Create()
            {
                return false;
            }

            /// <summary>
            /// Update an existing event
            /// </summary>
            /// <returns>A status flag</returns>
            /// <remarks>
            /// This will also update any rule changes
            /// </remarks>
            public bool Update()
            {
                return false;
            }
            /// <summary>
            /// Delete an event
            /// </summary>
            /// <returns>A status flag</returns>
            /// <remarks>
            /// This will also delete any rule sets
            /// </remarks>
            public bool Delete()
            {
                return false;
            }

            /// <summary>
            /// Serialize this Event as an XML document.
            /// </summary>
            /// <returns></returns>
            public XmlDocument Fetch()
            {
                return new XmlDocument();
            }

            /// <summary>
            /// Fetch the given number of recurrences from the beginning
            /// </summary>
            /// <param name="limit">The number of recurrences to fetch</param>
            /// <returns>A serialized version of these occurrences</returns>
            /// <remarks>
            /// Each occurrence is serialized with the following fields:
            /// - year: The year this event will occur
            /// - month: The month this event will occur
            /// - day: The day this event will occur
            /// </remarks>
            public XmlDocument FetchOccurrences(ulong limit)
            {
                return FetchOccurrences(limit, StartDate);
            }
            /// <summary>
            /// Fetch the given number of recurrences, starting at the given start date
            /// </summary>
            /// <param name="limit">The number of occurrences to fetch</param>
            /// <param name="start">The start date</param>
            /// <returns>A serialized version of these occurrences</returns>
            /// <remarks>
            /// Each occurrence is serialized with the following fields:
            /// - year: The year this event will occur
            /// - month: The month this event will occur
            /// - day: The day this event will occur
            /// </remarks>
            public XmlDocument FetchOccurrences(ulong limit, DateTime start)
            {
                return new XmlDocument();
            }
            /// <summary>
            /// Fetch all occurrences starting at the given start date and ending at the given stop date
            /// </summary>
            /// <param name="start">The date this fetch should start at</param>
            /// <param name="stop">The date this fetch should stop at</param>
            /// <returns>A serialized version of these occurrences</returns>
            /// <remarks>
            /// Each occurrence is serialized with the following fields:
            /// - year: The year this event will occur
            /// - month: The month this event will occur
            /// - day: The day this event will occur
            /// </remarks>
            public XmlDocument FetchOccurrences(DateTime start, DateTime stop)
            {
                return new XmlDocument();
            }
        }
    }
}