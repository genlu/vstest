// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.ObjectModel
{
    using System;
    using System.Diagnostics;

    /// <summary>
    /// Wrapper class for tracing.
    ///     - Shortcut-methods for Error, Warning, Info, Verbose.
    ///     - Adds additional information to the trace: calling process name, PID, ThreadID, Time.
    ///     - Uses custom switch <c>EqtTraceLevel</c> from .config file.
    ///     - By default tracing if OFF.
    ///     - Our build environment always sets the /d:TRACE so this class is always enabled,
    ///       the Debug class is enabled only in debug builds (/d:DEBUG).
    ///     - We ignore exceptions thrown by underlying TraceSwitch (e.g. due to config file error).
    ///       We log ignored exceptions to system Application log.
    ///       We pass through exceptions thrown due to incorrect arguments to <c>EqtTrace</c> methods.
    /// Usage: <c>EqtTrace.Info("Here's how to trace info");</c>
    /// </summary>
    public class PlatformEqtTrace : IPlatformEqtTrace
    {
        public static string ErrorOnInitialization { get; set; }

        public void WriteLine(PlatformTraceLevel level, string message)
        {
        }

        public bool InitializeVerboseTrace(string customLogFile)
        {
            return false;
        }

        public bool ShouldTrace(PlatformTraceLevel traceLevel)
        {
            return false;
        }

        public string GetLogFile()
        {
            return string.Empty;
        }

        public void SetTraceLevel(PlatformTraceLevel value)
        {
        }

        public PlatformTraceLevel GetTraceLevel()
        {
            return PlatformTraceLevel.Off;
        }
    }
}