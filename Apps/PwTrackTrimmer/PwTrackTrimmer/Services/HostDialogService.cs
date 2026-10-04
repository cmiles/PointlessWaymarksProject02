using System;

namespace PwTrackTrimmer.Services
{
    /// <summary>
    /// Coordinates Process + File Contract host dialog interop between PwTrackTrimmer
    /// and a calling external .NET desktop parent application.
    /// </summary>
    public static class HostDialogService
    {
        /// <summary>
        /// True when the editor is launched as a modal dialog from an external parent application.
        /// </summary>
        public static bool IsDialogMode { get; set; }

        /// <summary>
        /// Path to the input track file provided by the parent application.
        /// </summary>
        public static string InputFilePath { get; set; }

        /// <summary>
        /// Path where the final edited track should be written upon confirmation.
        /// </summary>
        public static string OutputFilePath { get; set; }

        /// <summary>
        /// Whether the user confirmed edits.
        /// </summary>
        public static bool IsConfirmed { get; set; }

        /// <summary>
        /// Process exit code to return: 0 for confirmed, 1 for cancelled / closed without saving.
        /// </summary>
        public static int ExitCode { get; set; } = 1;

        /// <summary>
        /// Callback invoked when user confirms dialog edits (used by Photino host to close window).
        /// </summary>
        public static Action OnConfirm { get; set; }

        /// <summary>
        /// Callback invoked when user cancels dialog edits (used by Photino host to close window).
        /// </summary>
        public static Action OnCancel { get; set; }
    }
}
