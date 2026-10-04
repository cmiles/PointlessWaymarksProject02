using System;
using System.IO;

namespace PwTrackTrimmer.Services
{
    public static class FileDownloadHelper
    {
        public static void TriggerBrowserDownload(string fileName, byte[] data, string mimeType = "application/octet-stream")
        {
            string base64 = Convert.ToBase64String(data);
            string js = $@"
                (function() {{
                    var byteCharacters = atob('{base64}');
                    var byteNumbers = new Array(byteCharacters.length);
                    for (var i = 0; i < byteCharacters.length; i++) {{
                        byteNumbers[i] = byteCharacters.charCodeAt(i);
                    }}
                    var byteArray = new Uint8Array(byteNumbers);
                    var blob = new Blob([byteArray], {{type: '{mimeType}'}});
                    var link = document.createElement('a');
                    link.href = window.URL.createObjectURL(blob);
                    link.download = '{fileName}';
                    document.body.appendChild(link);
                    link.click();
                    document.body.removeChild(link);
                    window.URL.revokeObjectURL(link.href);
                }})();
            ";

            OpenSilver.Interop.ExecuteJavaScriptVoid(js);
        }
    }
}
