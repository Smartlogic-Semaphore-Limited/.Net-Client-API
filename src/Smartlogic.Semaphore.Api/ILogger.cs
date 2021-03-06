﻿using System;

namespace Smartlogic.Semaphore.Api
{
    /// <summary>
    /// By providing a custom class that implements the ILogger interface, callers can  receive and process log messages in a manner that is appropriate for their application.
    /// </summary>
    public interface ILogger
    {
        /// <summary>
        /// The WriteError method is called when an expected error occurs.
        /// </summary>
        /// <param name="message">Contains a format string</param>
        /// <param name="parameters">Contains replacement paramters for the format string passed in message or null if no paramters are required</param>
        void WriteError(string message, params object[] parameters);

        /// <summary>
        /// The WriteException method is called when an unexpected error occurs.
        /// </summary>
        /// <param name="ex"></param>
        void WriteException(Exception ex);
        /// <summary>
        /// The WriteHigh method is called to log and important event.
        /// </summary>
        /// <param name="message">Contains a format string</param>
        /// <param name="parameters">Contains replacement paramters for the format string passed in message or null if no paramters are required</param>
        void WriteHigh(string message, params object[] parameters);
        /// <summary>
        /// The WriteLow method is called to log low level information/debug level events.
        /// </summary>
        /// <param name="message">Contains a format string</param>
        /// <param name="parameters">Contains replacement paramters for the format string passed in message or null if no paramters are required</param>
        void WriteLow(string message, params object[] parameters);

        /// <summary>
        /// The WriteMedium method is called to log significant events that occur during processing.
        /// </summary>
        /// <param name="message">Contains a format string</param>
        /// <param name="parameters">Contains replacement paramters for the format string passed in message or null if no paramters are required</param>
        void WriteMedium(string message, params object[] parameters);
    }
}