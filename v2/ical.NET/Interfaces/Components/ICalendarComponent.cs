﻿using ical.net.General;
using ical.net.Interfaces.General;

namespace ical.net.Interfaces.Components
{
    public interface ICalendarComponent : ICalendarObject
    {
        CalendarPropertyList Properties { get; }
        /// <summary>
        /// The name of the calendar object.
        /// Every calendar object can be assigned
        /// a name.
        /// </summary>
        string Name { get; set; }

        /// <summary>
        /// Returns the parent of this object.
        /// </summary>
        ICalendarObject Parent { get; set; }

        /// <summary>
        /// Returns a collection of children of this object.
        /// </summary>
        ICalendarObjectList<ICalendarObject> Children { get; }

        /// <summary>
        /// Returns the iCalendar that this object
        /// is associated with.
        /// </summary>
        ICalendar Calendar { get; }

        ICalendar ICalendar { get; }

        /// <summary>
        /// Returns the line number where this calendar
        /// object was found during parsing.
        /// </summary>
        int Line { get; set; }

        /// <summary>
        /// Returns the column number where this calendar
        /// object was found during parsing.
        /// </summary>
        int Column { get; set; }
    }
}