using System;
using System.IO;

namespace PwTrackTrimmer.Services
{
    public static class FileDropHelper
    {
        private static bool _isInitialized = false;

        /// <summary>
        /// Registers DOM-level HTML5 drag and drop event listeners on the window object.
        /// Works seamlessly across Web (WASM) and Desktop (Photino WebView2).
        /// </summary>
        public static void Initialize(Action<string, byte[]> onFileDropped, Action onDragEnter = null, Action onDragLeave = null)
        {
            if (_isInitialized) return;
            _isInitialized = true;

            Action<string, string> internalOnFileDropped = (fileName, base64Data) =>
            {
                try
                {
                    if (string.IsNullOrEmpty(base64Data)) return;
                    byte[] bytes = Convert.FromBase64String(base64Data);
                    onFileDropped?.Invoke(fileName, bytes);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[FileDropHelper] Error decoding dropped file: {ex.Message}");
                }
            };

            string script = @"
                (function(onDropCb, onEnterCb, onLeaveCb) {
                    if (window.__pwFileDropInitialized) return;
                    window.__pwFileDropInitialized = true;

                    var dragCounter = 0;

                    function hasFiles(e) {
                        if (!e.dataTransfer || !e.dataTransfer.types) return false;
                        var types = e.dataTransfer.types;
                        if (typeof types.includes === 'function') return types.includes('Files');
                        if (typeof types.contains === 'function') return types.contains('Files');
                        if (typeof types.indexOf === 'function') return types.indexOf('Files') !== -1;
                        return false;
                    }

                    window.addEventListener('dragenter', function(e) {
                        e.preventDefault();
                        if (hasFiles(e)) {
                            dragCounter++;
                            if (dragCounter === 1 && onEnterCb) {
                                try { onEnterCb(); } catch(err) { console.error(err); }
                            }
                        }
                    }, false);

                    window.addEventListener('dragover', function(e) {
                        e.preventDefault();
                        if (e.dataTransfer) {
                            e.dataTransfer.dropEffect = 'copy';
                        }
                    }, false);

                    window.addEventListener('dragleave', function(e) {
                        e.preventDefault();
                        if (hasFiles(e)) {
                            dragCounter--;
                            if (dragCounter <= 0) {
                                dragCounter = 0;
                                if (onLeaveCb) {
                                    try { onLeaveCb(); } catch(err) { console.error(err); }
                                }
                            }
                        }
                    }, false);

                    window.addEventListener('drop', function(e) {
                        e.preventDefault();
                        dragCounter = 0;
                        if (onLeaveCb) {
                            try { onLeaveCb(); } catch(err) { console.error(err); }
                        }

                        if (!e.dataTransfer || !e.dataTransfer.files || e.dataTransfer.files.length === 0) {
                            return;
                        }

                        var file = e.dataTransfer.files[0];
                        var fileName = file.name || 'track.gpx';

                        var reader = new FileReader();
                        reader.onload = function(evt) {
                            try {
                                var arrayBuffer = evt.target.result;
                                var bytes = new Uint8Array(arrayBuffer);
                                var binary = '';
                                var len = bytes.byteLength;
                                var chunkSize = 32768;
                                for (var i = 0; i < len; i += chunkSize) {
                                    var chunk = bytes.subarray(i, Math.min(i + chunkSize, len));
                                    binary += String.fromCharCode.apply(null, chunk);
                                }
                                var base64 = window.btoa(binary);
                                if (onDropCb) {
                                    onDropCb(fileName, base64);
                                }
                            } catch (readErr) {
                                console.error('[FileDropHelper] Error reading dropped file content:', readErr);
                            }
                        };
                        reader.onerror = function(err) {
                            console.error('[FileDropHelper] FileReader error:', err);
                        };
                        reader.readAsArrayBuffer(file);
                    }, false);

                    window.addEventListener('blur', function() {
                        dragCounter = 0;
                        if (onLeaveCb) {
                            try { onLeaveCb(); } catch(err) {}
                        }
                    }, false);
                })($0, $1, $2);
            ";

            OpenSilver.Interop.ExecuteJavaScriptVoid(script, internalOnFileDropped, onDragEnter, onDragLeave);
        }
    }
}
