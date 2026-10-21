/**
 * PwImageMetadataViewer - Core JavaScript Bridge
 * Integrates WebAssembly ExifTool, Leaflet Map with Gesture Isolation,
 * Drag-and-Drop, File Picking, RAW Preview Extraction, and OpenSilver Bridge.
 */
(function () {
    if (window.pwExifBridge) return;

    let exifModule = null;
    let leafletMapInstance = null;
    let leafletIsolationHandle = null;
    let csharpCallback = null;
    let isProcessing = false;
    let currentMetadata = null;

    // Helper: Parse DMS or decimal coordinates to signed decimal degrees
    function parseCoordinate(value, ref) {
        if (value === undefined || value === null) return null;
        if (typeof value === 'number') {
            let num = value;
            if (ref && (ref === 'S' || ref === 'W') && num > 0) num = -num;
            return num;
        }
        let str = String(value).trim();
        // Check for DMS pattern: e.g. 37 deg 46' 29.88" N
        let dmsMatch = str.match(/([0-9.]+)\s*deg(?:rees)?\s*([0-9.]+)'?\s*([0-9.]+)?\"?\s*([NSEW])?/i);
        if (dmsMatch) {
            let deg = parseFloat(dmsMatch[1]) || 0;
            let min = parseFloat(dmsMatch[2]) || 0;
            let sec = parseFloat(dmsMatch[3]) || 0;
            let dir = (dmsMatch[4] || ref || '').toUpperCase();
            let dec = deg + min / 60 + sec / 3600;
            if (dir === 'S' || dir === 'W') dec = -dec;
            return dec;
        }
        // Direct decimal string
        let decVal = parseFloat(str);
        if (!isNaN(decVal)) {
            let dir = (ref || '').toUpperCase();
            if (dir === 'S' || dir === 'W') {
                if (decVal > 0) decVal = -decVal;
            }
            return decVal;
        }
        return null;
    }

    // Helper: Format decimal coordinates to pretty DMS string
    function formatCoordDms(degVal, isLat) {
        if (degVal === null || degVal === undefined || isNaN(degVal)) return '';
        let dir = isLat ? (degVal >= 0 ? 'N' : 'S') : (degVal >= 0 ? 'E' : 'W');
        let abs = Math.abs(degVal);
        let deg = Math.floor(abs);
        let minFloat = (abs - deg) * 60;
        let min = Math.floor(minFloat);
        let sec = ((minFloat - min) * 60).toFixed(2);
        return `${deg}° ${min}' ${sec}" ${dir} (${degVal.toFixed(6)}°)`;
    }

    // Helper: Format file size in human-readable form
    function formatBytes(bytes) {
        if (!bytes || isNaN(bytes)) return '0 B';
        const units = ['B', 'KB', 'MB', 'GB', 'TB'];
        let i = 0;
        let size = Number(bytes);
        while (size >= 1024 && i < units.length - 1) {
            size /= 1024;
            i++;
        }
        return `${size.toFixed(i === 0 ? 0 : 2)} ${units[i]}`;
    }

    // Helper: Format focal length to only show decimals if there are non-zero values
    function formatFocalLength(val) {
        if (val === undefined || val === null) return '';
        let str = String(val).trim();
        let m = str.match(/^([0-9]+(?:\.[0-9]+)?)\s*(mm)?$/i);
        if (m) {
            let num = parseFloat(m[1]);
            if (!isNaN(num)) {
                let numStr = (num % 1 === 0) ? String(Math.round(num)) : String(parseFloat(num.toFixed(2)));
                return `${numStr} mm`;
            }
        }
        return str;
    }

    // Get absolute URL for the wasm binary
    function getWasmUrl() {
        // Look for relative to applibs/exiftool/
        let base = document.baseURI || window.location.href;
        return new URL('applibs/exiftool/zeroperl-mqcadjqm.wasm', base).href;
    }

    // Lazily load ExifTool module
    async function getExifModule() {
        if (exifModule) return exifModule;
        try {
            let base = document.baseURI || window.location.href;
            let moduleUrl = new URL('applibs/exiftool/index.js', base).href;
            exifModule = await import(moduleUrl);
            return exifModule;
        } catch (err) {
            console.error('[PwExifBridge] Failed to import ExifTool module:', err);
            throw err;
        }
    }

    // Ensure the input passed to ExifTool is a valid File or { name, data } object
    function ensureExifInput(fileObj, fileName) {
        const safeName = (fileName || (fileObj && fileObj.name) || 'image.jpg').replace(/[/\\]/g, '_');
        if (typeof File !== 'undefined' && fileObj instanceof Blob) {
            if (fileObj instanceof File && fileObj.name) {
                return fileObj;
            }
            try {
                return new File([fileObj], safeName, { type: fileObj.type || 'image/jpeg' });
            } catch (e) {
                return { name: safeName, data: fileObj };
            }
        }
        if (fileObj && typeof fileObj === 'object' && fileObj.name && fileObj.data !== undefined) {
            return fileObj;
        }
        if (fileObj instanceof Blob) {
            return { name: safeName, data: fileObj };
        }
        return { name: safeName, data: fileObj };
    }

    let activeBlobUrls = [];
    function createTrackedBlobUrl(blob) {
        if (!blob) return null;
        const url = URL.createObjectURL(blob);
        activeBlobUrls.push(url);
        while (activeBlobUrls.length > 5) {
            const old = activeBlobUrls.shift();
            try { URL.revokeObjectURL(old); } catch (e) { }
        }
        return url;
    }

    // Inspect first bytes to verify the data is a browser-renderable image
    function detectImageMimeType(bytes) {
        if (!bytes || bytes.length < 4) return null;
        // JPEG: FF D8 FF
        if (bytes[0] === 0xFF && bytes[1] === 0xD8 && bytes[2] === 0xFF) {
            return 'image/jpeg';
        }
        // PNG: 89 50 4E 47
        if (bytes[0] === 0x89 && bytes[1] === 0x50 && bytes[2] === 0x4E && bytes[3] === 0x47) {
            return 'image/png';
        }
        // GIF: 47 49 46 38
        if (bytes[0] === 0x47 && bytes[1] === 0x49 && bytes[2] === 0x46 && bytes[3] === 0x38) {
            return 'image/gif';
        }
        // WebP: RIFF....WEBP
        if (bytes.length >= 12 &&
            bytes[0] === 0x52 && bytes[1] === 0x49 && bytes[2] === 0x46 && bytes[3] === 0x46 &&
            bytes[8] === 0x57 && bytes[9] === 0x45 && bytes[10] === 0x42 && bytes[11] === 0x50) {
            return 'image/webp';
        }
        // BMP: 42 4D
        if (bytes[0] === 0x42 && bytes[1] === 0x4D) {
            return 'image/bmp';
        }
        return null;
    }

    function uint8ArrayToBase64(bytes) {
        let binary = '';
        const len = bytes.byteLength;
        const chunkSize = 8192;
        for (let i = 0; i < len; i += chunkSize) {
            const chunk = bytes.subarray(i, Math.min(i + chunkSize, len));
            binary += String.fromCharCode.apply(null, chunk);
        }
        return btoa(binary);
    }

    function blobToDataUrl(blob) {
        return new Promise((resolve) => {
            if (!blob) return resolve(null);
            const reader = new FileReader();
            reader.onloadend = () => {
                if (typeof reader.result === 'string') {
                    resolve(reader.result);
                } else {
                    resolve(null);
                }
            };
            reader.onerror = () => {
                resolve(null);
            };
            reader.readAsDataURL(blob);
        });
    }

    // Converts a Blob into a preview URL. In Photino/WebView2 (file: protocol), Chromium blocks
    // blob:file:/// URLs in <img> tags; therefore Data URLs are required.
    async function createPreviewUrl(blob, fallbackMime) {
        if (!blob) return null;
        const isFileProtocol = (typeof window !== 'undefined' && window.location && window.location.protocol === 'file:');

        if (isFileProtocol) {
            try {
                const dataUrl = await blobToDataUrl(blob);
                if (dataUrl) return dataUrl;
            } catch (e) {
                console.warn('[PwExifBridge] blobToDataUrl failed under file: protocol:', e);
            }
        }

        // Under http: / https: (or as fallback), createTrackedBlobUrl is fast and memory-efficient
        try {
            return createTrackedBlobUrl(blob);
        } catch (e) {
            return await blobToDataUrl(blob);
        }
    }

    // Extract preview image from RAW files or return blob/data URL for regular images
    async function getPreviewUrl(fileObj, fileName, mimeType) {
        const lowerName = (fileName || (fileObj && fileObj.name) || '').toLowerCase();
        const lowerMime = (mimeType || (fileObj && fileObj.type) || '').toLowerCase();

        // Detect if this is a RAW, TIFF, or non-browser-renderable format
        const isRawExt = /\.(cr2|cr3|nef|nrw|arw|dng|raf|rw2|orf|pef|srw|srf|raw|kdc|dcr|mos|mrw|erf|mef|x3f|3fr|fff|rwl|iiq|rwz|tif|tiff|heic|heif)$/i.test(lowerName);
        const isRawMime = lowerMime.includes('arw') || lowerMime.includes('raw') || lowerMime.includes('tiff') || lowerMime.includes('tif') ||
            lowerMime.includes('cr2') || lowerMime.includes('cr3') || lowerMime.includes('nef') || lowerMime.includes('dng') ||
            lowerMime.includes('raf') || lowerMime.includes('rw2') || lowerMime.includes('orf') || lowerMime.includes('pef') ||
            lowerMime.includes('heic') || lowerMime.includes('heif');

        const isStandardExt = /\.(jpe?g|png|webp|gif|bmp|svg|avif)$/i.test(lowerName);
        const isStandardMime = /^(image\/(jpeg|jpg|png|webp|gif|bmp|svg\+xml|avif))$/i.test(lowerMime);
        const isStandard = (isStandardExt || isStandardMime) && !isRawExt && !isRawMime;

        // Fast path 1: Cached base64 data from loadFromBase64 (ONLY for standard web images)
        if (fileObj && fileObj._base64Preview && isStandard) {
            const inferredMime = isStandardMime ? lowerMime : (lowerName.endsWith('.png') ? 'image/png' : 'image/jpeg');
            return `data:${inferredMime};base64,${fileObj._base64Preview}`;
        }

        // Fast path 2: Standard web image -> return preview URL (Data URL on file:, blob URL on http:)
        if (isStandard && fileObj instanceof Blob) {
            try {
                return await createPreviewUrl(fileObj, isStandardMime ? lowerMime : 'image/jpeg');
            } catch (e) {
                console.warn('[PwExifBridge] createPreviewUrl failed for standard image:', e);
            }
        }

        // 2. RAW or TIFF images: extract embedded JPEG preview via ExifTool -b
        try {
            const m = await getExifModule();
            const wasmUrl = getWasmUrl();
            // PreviewImage is primary for Sony ARW, Canon CR2/CR3, DNG, etc.
            // JpgFromRaw is primary for Sony ARW, Nikon NEF, etc.
            const previewFlags = ['-PreviewImage', '-JpgFromRaw', '-OtherImage', '-ThumbnailImage'];
            const exifInput = ensureExifInput(fileObj, fileName);

            for (const flag of previewFlags) {
                try {
                    const res = await m.parseMetadata(exifInput, {
                        fetch: () => fetch(wasmUrl),
                        args: ['-b', flag, '-m', '-q', '-q'],
                        binary: true
                    });

                    if (res && res.success && res.data && res.data.length > 100) {
                        const detectedMime = detectImageMimeType(res.data);
                        if (detectedMime) {
                            const blob = new Blob([res.data], { type: detectedMime });
                            return await createPreviewUrl(blob, detectedMime);
                        } else {
                            console.warn(`[PwExifBridge] Tag ${flag} returned ${res.data.length} bytes, but header magic bytes did not match a browser image.`);
                        }
                    }
                } catch (e) {
                    // Ignore and try next tag
                }
            }
        } catch (mErr) {
            console.error('[PwExifBridge] Error initializing ExifTool for preview extraction:', mErr);
        }

        // Standard images or standard blobs fallback only (NEVER return raw camera files)
        if (fileObj instanceof Blob && isStandard) {
            try {
                return await createPreviewUrl(fileObj, 'image/jpeg');
            } catch (e) { }
        }

        return null;
    }


    // Main extraction function
    async function processFile(fileObj, fileName, explicitFileSize) {
        if (isProcessing) return;
        isProcessing = true;

        const effectiveName = fileName || (fileObj && fileObj.name) || 'image.jpg';

        if (window.pwExifBridge.onStatusUpdate) {
            window.pwExifBridge.onStatusUpdate('Extracting metadata with WebAssembly ExifTool...', true);
        }

        try {
            const m = await getExifModule();
            const wasmUrl = getWasmUrl();
            const exifInput = ensureExifInput(fileObj, effectiveName);

            // Run ExifTool with -json and -G1 for grouped metadata
            const res = await m.parseMetadata(exifInput, {
                fetch: () => fetch(wasmUrl),
                args: ['-json', '-G1', '-a', '-s']
            });

            if (!res || !res.success || !res.data) {
                const errMsg = res && res.error ? res.error : 'The file format is not supported or ExifTool could not extract metadata.';
                notifyError(errMsg, fileName);
                return;
            }

            let parsedList;
            try {
                parsedList = typeof res.data === 'string' ? JSON.parse(res.data) : res.data;
            } catch (pe) {
                notifyError('Failed to parse ExifTool output.', fileName);
                return;
            }

            if (!Array.isArray(parsedList) || parsedList.length === 0) {
                notifyError('No metadata tags found in this file.', fileName);
                return;
            }

            const rawTags = parsedList[0];
            const size = explicitFileSize || (fileObj instanceof Blob ? fileObj.size : rawTags['System:FileSize'] || 0);

            // Extract preview image
            const mimeType = rawTags['File:MIMEType'] || rawTags['MIMEType'] || '';
            const previewUrl = await getPreviewUrl(fileObj, fileName, mimeType);
            if (previewUrl) {
                displayPreview(null, previewUrl);
            }

            // Extract GPS data
            let lat = parseCoordinate(rawTags['Composite:GPSLatitude'] || rawTags['GPS:GPSLatitude'], rawTags['GPS:GPSLatitudeRef']);
            let lng = parseCoordinate(rawTags['Composite:GPSLongitude'] || rawTags['GPS:GPSLongitude'], rawTags['GPS:GPSLongitudeRef']);
            let alt = null;
            let altRaw = rawTags['Composite:GPSAltitude'] || rawTags['GPS:GPSAltitude'];
            if (altRaw !== undefined && altRaw !== null) {
                let altMatch = String(altRaw).match(/([0-9.-]+)/);
                if (altMatch) {
                    alt = parseFloat(altMatch[1]);
                    if (rawTags['GPS:GPSAltitudeRef'] === 1 || rawTags['GPS:GPSAltitudeRef'] === '1' || String(altRaw).toLowerCase().includes('below')) {
                        alt = -Math.abs(alt);
                    }
                }
            }

            const hasGps = (lat !== null && lng !== null && !isNaN(lat) && !isNaN(lng));

            // Compile tag list
            const allTags = [];
            for (const [key, val] of Object.entries(rawTags)) {
                if (key === 'SourceFile') continue;
                let group = 'General';
                let tagName = key;
                if (key.includes(':')) {
                    const parts = key.split(':');
                    group = parts[0];
                    tagName = parts.slice(1).join(':');
                }
                let displayVal = val;
                if (Array.isArray(val)) {
                    displayVal = val.join(', ');
                } else if (typeof val === 'object' && val !== null) {
                    displayVal = JSON.stringify(val);
                } else {
                    displayVal = String(val);
                }

                allTags.push({
                    rawKey: key,
                    group: group,
                    tag: tagName,
                    value: displayVal
                });
            }

            // Helper to ensure values are string
            const toStr = (v) => {
                if (v === null || v === undefined) return '';
                if (Array.isArray(v)) return v.join(', ');
                if (typeof v === 'object') return JSON.stringify(v);
                return String(v).trim();
            };

            const artistVal = rawTags['IFD0:Artist'] || rawTags['Artist'] || rawTags['XMP-dc:Creator'] || rawTags['Creator'] || rawTags['IPTC:By-line'] || rawTags['By-line'] || rawTags['ExifIFD:OwnerName'] || rawTags['OwnerName'] || rawTags['XMP-photoshop:Author'] || rawTags['Author'];
            const copyrightVal = rawTags['IFD0:Copyright'] || rawTags['Copyright'] || rawTags['XMP-dc:Rights'] || rawTags['Rights'] || rawTags['IPTC:CopyrightNotice'] || rawTags['CopyrightNotice'] || rawTags['XMP-xmpRights:Marked'] || rawTags['XMP-cc:license'];
            const creditVal = rawTags['IPTC:Credit'] || rawTags['Credit'] || rawTags['IPTC:Source'] || rawTags['Source'] || rawTags['XMP-photoshop:Credit'] || rawTags['XMP-photoshop:Source'] || rawTags['Photoshop:Credit'];
            const captionVal = rawTags['IFD0:ImageDescription'] || rawTags['ImageDescription'] || rawTags['XMP-dc:Description'] || rawTags['Description'] || rawTags['IPTC:Caption-Abstract'] || rawTags['Caption-Abstract'] || rawTags['ExifIFD:UserComment'] || rawTags['UserComment'];
            const titleVal = rawTags['XMP-dc:Title'] || rawTags['Title'] || rawTags['IPTC:ObjectName'] || rawTags['ObjectName'] || rawTags['IPTC:Headline'] || rawTags['Headline'];
            const tagsKeywordsVal = rawTags['XMP-dc:Subject'] || rawTags['Subject'] || rawTags['IPTC:Keywords'] || rawTags['Keywords'] || rawTags['XMP:Keywords'] || rawTags['XMP-lr:HierarchicalSubject'] || rawTags['XPKeywords'];

            // Summary values
            const report = {
                success: true,
                fileName: toStr(fileName || rawTags['System:FileName'] || 'Image'),
                fileSize: size,
                fileSizeFormatted: formatBytes(size),
                mimeType: toStr(mimeType),
                fileType: toStr(rawTags['File:FileType'] || rawTags['FileType']),
                make: toStr(rawTags['IFD0:Make'] || rawTags['Make']),
                model: toStr(rawTags['IFD0:Model'] || rawTags['Model']),
                lens: toStr(rawTags['ExifIFD:LensModel'] || rawTags['Composite:LensSpec'] || rawTags['LensModel']),
                dateTime: toStr(rawTags['ExifIFD:DateTimeOriginal'] || rawTags['CreateDate'] || rawTags['DateTimeOriginal']),
                exposureTime: toStr(rawTags['ExifIFD:ExposureTime'] || rawTags['ExposureTime']),
                fNumber: toStr(rawTags['ExifIFD:FNumber'] || rawTags['FNumber']),
                iso: toStr(rawTags['ExifIFD:ISO'] || rawTags['ISO']),
                focalLength: formatFocalLength(rawTags['ExifIFD:FocalLength'] || rawTags['FocalLength']),
                width: toStr(rawTags['File:ImageWidth'] || rawTags['ImageWidth']),
                height: toStr(rawTags['File:ImageHeight'] || rawTags['ImageHeight']),
                megapixels: toStr(rawTags['Composite:Megapixels'] || rawTags['Megapixels']),
                hasGps: hasGps,
                latitude: lat,
                longitude: lng,
                altitude: alt,
                latitudeFormatted: hasGps ? formatCoordDms(lat, true) : '',
                longitudeFormatted: hasGps ? formatCoordDms(lng, false) : '',
                altitudeFormatted: (alt !== null && !isNaN(alt)) ? `${alt.toFixed(1)} m (${(alt * 3.28084).toFixed(0)} ft)` : '',
                previewUrl: previewUrl || '',
                artist: toStr(artistVal),
                copyright: toStr(copyrightVal),
                credit: toStr(creditVal),
                caption: toStr(captionVal),
                title: toStr(titleVal),
                tagsOrKeywords: toStr(tagsKeywordsVal),
                tags: allTags
            };

            currentMetadata = report;

            if (window.pwExifBridge.onStatusUpdate) {
                window.pwExifBridge.onStatusUpdate('Metadata successfully extracted', false);
            }

            // Notify C# and update map if GPS available
            notifyResult(report);

            if (hasGps) {
                const label = `${report.make} ${report.model}`.trim() || report.fileName;
                const popup = `<b>${escapeHtml(label)}</b><br/>${report.latitudeFormatted}<br/>${report.longitudeFormatted}` +
                    (report.altitudeFormatted ? `<br/>Alt: ${report.altitudeFormatted}` : '');
                renderMap(lat, lng, popup);
            } else {
                hideMap();
            }

        } catch (err) {
            console.error('[PwExifBridge] Error processing file:', err);
            notifyError(err.message || 'Error occurred while analyzing file with ExifTool.', fileName);
        } finally {
            isProcessing = false;
        }
    }

    function escapeHtml(str) {
        if (!str) return '';
        return String(str)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    }

    function notifyResult(report) {
        const jsonStr = JSON.stringify(report);
        if (csharpCallback) {
            try {
                csharpCallback(jsonStr);
            } catch (e) {
                console.error('[PwExifBridge] csharpCallback error:', e);
            }
        }
        // Also dispatch window event
        window.dispatchEvent(new CustomEvent('pw-metadata-loaded', { detail: report }));
    }

    function notifyError(errMsg, fileName) {
        const errObj = {
            success: false,
            fileName: fileName || '',
            error: errMsg
        };
        currentMetadata = errObj;
        if (window.pwExifBridge.onStatusUpdate) {
            window.pwExifBridge.onStatusUpdate(errMsg, false, true);
        }
        if (csharpCallback) {
            try {
                csharpCallback(JSON.stringify(errObj));
            } catch (e) {
                console.error('[PwExifBridge] csharpCallback error:', e);
            }
        }
        window.dispatchEvent(new CustomEvent('pw-metadata-error', { detail: errObj }));
        hideMap();
    }

    let activePreviewContainer = null;
    let activeMapContainer = null;

    function setContainers(previewEl, mapEl) {
        if (previewEl) {
            activePreviewContainer = previewEl;
            const allWrappers = document.querySelectorAll('#pw-preview-wrapper');
            allWrappers.forEach(w => {
                if (w.parentElement !== activePreviewContainer) {
                    try { w.remove(); } catch (e) { }
                }
            });
        }
        if (mapEl) {
            activeMapContainer = mapEl;
            const allMaps = document.querySelectorAll('.pw-leaflet-div, #pw-leaflet-map');
            allMaps.forEach(m => {
                if (m.parentElement !== activeMapContainer) {
                    try {
                        if (leafletMapInstance) {
                            safeRemoveMap(leafletMapInstance);
                            leafletMapInstance = null;
                            window.leafletMapInstance = null;
                        }
                        m.remove();
                    } catch (e) { }
                }
            });
        }
    }

    // Helper to safely mount or retrieve map container DOM element
    function ensureMapContainer(containerElement) {
        const container = containerElement || activeMapContainer;
        if (!container) {
            const existing = document.getElementById('pw-leaflet-map');
            if (existing && existing.isConnected) return existing;
            return null;
        }

        const oldMap = document.getElementById('pw-leaflet-map');
        if (oldMap && oldMap.parentElement !== container) {
            try {
                if (leafletMapInstance) {
                    safeRemoveMap(leafletMapInstance);
                    leafletMapInstance = null;
                    window.leafletMapInstance = null;
                }
                oldMap.remove();
            } catch (e) { }
        }

        var mapDiv = container.querySelector('.pw-leaflet-div') || container.querySelector('#pw-leaflet-map');
        if (!mapDiv) {
            mapDiv = document.createElement('div');
            mapDiv.className = 'pw-leaflet-div';
            mapDiv.id = 'pw-leaflet-map';
            mapDiv.style.position = 'absolute';
            mapDiv.style.inset = '0';
            mapDiv.style.width = '100%';
            mapDiv.style.height = '100%';
            mapDiv.style.overflow = 'hidden';
            mapDiv.style.zIndex = '1';
            mapDiv.style.borderRadius = '8px';
        }
        if (getComputedStyle(container).position === 'static') {
            container.style.position = 'relative';
        }
        if (mapDiv.parentElement !== container) {
            container.appendChild(mapDiv);
        }
        return mapDiv;
    }

    // Defensive loader: Ensure scripts/stylesheets are present even if not in index.html
    function ensureCss(href) {
        var links = document.getElementsByTagName('link');
        for (var i = 0; i < links.length; i++) {
            if (links[i].href && links[i].href.indexOf(href) !== -1) return;
        }
        var link = document.createElement('link');
        link.rel = 'stylesheet';
        link.href = href;
        document.head.appendChild(link);
    }
    function ensureScript(src) {
        var scripts = document.getElementsByTagName('script');
        for (var i = 0; i < scripts.length; i++) {
            if (scripts[i].src && scripts[i].src.indexOf(src) !== -1) return;
        }
        var s = document.createElement('script');
        s.src = src;
        document.head.appendChild(s);
    }

    function safeRemoveMap(map) {
        if (!map) return;
        try {
            if (map.gestureHandling) {
                map.gestureHandling._enableInteractions = function () { };
                try { map.gestureHandling.disable(); } catch (e) { }
            }
            if (map.dragging) {
                try { map.dragging.disable(); } catch (e) { }
            }
            if (map.scrollWheelZoom) {
                if (map.scrollWheelZoom._timer) {
                    clearTimeout(map.scrollWheelZoom._timer);
                    map.scrollWheelZoom._timer = null;
                }
                try { map.scrollWheelZoom.disable(); } catch (e) { }
            }
            if (map.touchZoom) {
                try { map.touchZoom.disable(); } catch (e) { }
            }
            map.remove();
        } catch (e) {
            console.warn('[PwExifBridge] Error removing map instance:', e);
        }
    }

    function cleanMapContainer(container) {
        if (!container) return;
        if (typeof L !== 'undefined' && L.DomEvent && L.DomEvent.off) {
            try { L.DomEvent.off(container); } catch (e) { }
        }
        container.innerHTML = '';
        try {
            delete container._leaflet_events;
            delete container._leaflet_id;
        } catch (e) {
            container._leaflet_events = undefined;
            container._leaflet_id = undefined;
        }
    }

    // Leaflet Map Rendering & Gesture Isolation (Matching LeafletScrollIsolationHelper)
    function renderMap(arg1, arg2, arg3, arg4) {
        let containerElement = null;
        let lat, lng, popupText;

        if (typeof arg1 === 'object' && arg1 !== null) {
            containerElement = arg1;
            lat = arg2;
            lng = arg3;
            popupText = arg4;
        } else {
            containerElement = activeMapContainer;
            lat = arg1;
            lng = arg2;
            popupText = arg3;
        }
        if (!containerElement) {
            containerElement = activeMapContainer;
        }

        if (lat === undefined || lng === undefined || lat === null || lng === null || isNaN(lat) || isNaN(lng)) {
            console.warn('[PwExifBridge] renderMap called with invalid coordinates:', lat, lng);
            return;
        }

        ensureCss('applibs/leaflet/leaflet.css');
        ensureCss('applibs/leaflet-gesture-handling.min.css');
        ensureCss('applibs/L.Control.Locate.min.css');

        if (typeof L === 'undefined') {
            ensureScript('applibs/leaflet/leaflet.js');
        }
        ensureScript('applibs/leaflet-gesture-handling.min.js');
        ensureScript('applibs/L.Control.Locate.min.js');

        // Configure local leaflet default icon assets
        if (typeof L !== 'undefined' && L.Icon && L.Icon.Default) {
            try {
                delete L.Icon.Default.prototype._getIconUrl;
                L.Icon.Default.mergeOptions({
                    iconRetinaUrl: 'applibs/leaflet/images/marker-icon-2x.png',
                    iconUrl: 'applibs/leaflet/images/marker-icon.png',
                    shadowUrl: 'applibs/leaflet/images/marker-shadow.png'
                });
            } catch (e) {
                L.Icon.Default.imagePath = 'applibs/leaflet/images/';
            }
        }

        var attempts = 0;
        function tryInit() {
            if (!containerElement) {
                containerElement = activeMapContainer;
            }
            if (typeof L === 'undefined' || !L.map) {
                if (++attempts < 160) setTimeout(tryInit, 50);
                return;
            }

            var container = ensureMapContainer(containerElement);
            if (!container || !container.isConnected || container.clientWidth < 30 || container.clientHeight < 30) {
                if (++attempts < 160) setTimeout(tryInit, 50);
                return;
            }

            container.style.display = 'block';

            if (leafletIsolationHandle) {
                try { leafletIsolationHandle.destroy(); } catch (e) { }
                leafletIsolationHandle = null;
            }

            var oldMap = leafletMapInstance || window.leafletMapInstance;
            if (oldMap) {
                safeRemoveMap(oldMap);
                leafletMapInstance = null;
                window.leafletMapInstance = null;
            }

            cleanMapContainer(container);

            try {
                // 1. Initialize Leaflet Map with gestureHandling: true
                leafletMapInstance = L.map(container, {
                    zoomControl: true,
                    attributionControl: false,
                    maxZoom: 22,
                    zoomSnap: 0,
                    preferCanvas: true,
                    gestureHandling: true
                }).setView([lat, lng], 14);

                // 2. Install LeafletScrollIsolationHelper
                if (window.pwLeafletGestureIsolation) {
                    leafletIsolationHandle = window.pwLeafletGestureIsolation.install(leafletMapInstance, container, {
                        scrollParent: window.pwMainScrollViewerElement
                    });
                }

                // Ensure map dragging is enabled for mouse interactions
                if (leafletMapInstance.dragging && !leafletMapInstance.dragging.enabled()) {
                    leafletMapInstance.dragging.enable();
                }

                // 3. Add L.Control.Locate
                if (L.control && L.control.locate) {
                    try {
                        L.control.locate({
                            position: 'topleft',
                            icon: 'leaflet-control-locate-location-arrow',
                            iconLoading: 'leaflet-control-locate-spinner',
                            strings: {
                                title: 'Show my location'
                            },
                            locateOptions: {
                                maxZoom: 18,
                                enableHighAccuracy: true
                            }
                        }).addTo(leafletMapInstance);
                    } catch (err) {
                        console.warn('Locate control initialization failed:', err);
                    }
                }

                // 4. Add Tile Layer
                L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
                    maxZoom: 22,
                    maxNativeZoom: 19
                }).addTo(leafletMapInstance);

                const marker = L.marker([lat, lng]).addTo(leafletMapInstance);
                if (popupText) {
                    marker.bindPopup(popupText).openPopup();
                }

                window.leafletMapInstance = leafletMapInstance;

                // Handle container resize automatically
                var parentHost = containerElement || container.parentElement || container;
                if (window.ResizeObserver && !parentHost._hasResizeObserver) {
                    parentHost._hasResizeObserver = true;
                    var ro = new ResizeObserver(function () {
                        if (leafletMapInstance) {
                            leafletMapInstance.invalidateSize();
                        }
                    });
                    ro.observe(parentHost);
                }

                setTimeout(() => {
                    if (leafletMapInstance) leafletMapInstance.invalidateSize();
                }, 100);
                setTimeout(() => {
                    if (leafletMapInstance) leafletMapInstance.invalidateSize();
                }, 300);
            } catch (err) {
                console.error('[PwExifBridge] Error initializing Leaflet map:', err);
            }
        }

        tryInit();
    }

    function hideMap(containerElement) {
        const container = document.getElementById('pw-leaflet-map') || (containerElement && containerElement.querySelector('.pw-leaflet-div'));
        if (container) {
            container.style.display = 'none';
        }
        if (leafletIsolationHandle) {
            try {
                leafletIsolationHandle.destroy();
            } catch (e) { }
            leafletIsolationHandle = null;
        }
        var oldMap = leafletMapInstance || window.leafletMapInstance;
        if (oldMap) {
            safeRemoveMap(oldMap);
            leafletMapInstance = null;
            window.leafletMapInstance = null;
        }
        if (container) {
            cleanMapContainer(container);
        }
    }

    function invalidateMapSize() {
        if (leafletMapInstance) {
            try {
                leafletMapInstance.invalidateSize();
            } catch (e) { }
        }
    }

    // External Map Actions
    function openOpenStreetMap(lat, lng) {
        if (lat === undefined || lng === undefined) {
            if (currentMetadata && currentMetadata.hasGps) {
                lat = currentMetadata.latitude;
                lng = currentMetadata.longitude;
            }
        }
        if (lat !== null && lng !== null && !isNaN(lat) && !isNaN(lng)) {
            const url = `https://www.openstreetmap.org/?mlat=${lat}&mlon=${lng}#map=16/${lat}/${lng}`;
            window.open(url, '_blank');
        }
    }

    function openPeakFinder(lat, lng, ele, name) {
        if (lat === undefined || lng === undefined) {
            if (currentMetadata && currentMetadata.hasGps) {
                lat = currentMetadata.latitude;
                lng = currentMetadata.longitude;
                ele = currentMetadata.altitude;
                name = currentMetadata.fileName;
            }
        }
        if (lat !== null && lng !== null && !isNaN(lat) && !isNaN(lng)) {
            let url = `https://www.peakfinder.org/?lat=${lat}&lng=${lng}`;
            if (ele !== null && ele !== undefined && !isNaN(ele)) {
                url += `&ele=${Math.round(ele)}`;
            }
            if (name) {
                url += `&name=${encodeURIComponent(name)}`;
            }
            window.open(url, '_blank');
        }
    }

    // File Input Helper
    function initFileInput() {
        let input = document.getElementById('pw-hidden-file-input');
        if (!input) {
            input = document.createElement('input');
            input.type = 'file';
            input.id = 'pw-hidden-file-input';
            input.style.display = 'none';
            input.accept = 'image/*,.cr2,.cr3,.nef,.arw,.dng,.raf,.rw2,.orf,.pef,.raw,.tif,.tiff,.heic,.heif,.avif';
            input.addEventListener('change', (e) => {
                if (e.target.files && e.target.files.length > 0) {
                    const file = e.target.files[0];
                    processFile(file, file.name);
                }
                input.value = '';
            });
            document.body.appendChild(input);
        }
    }

    function openFileDialog() {
        initFileInput();
        const input = document.getElementById('pw-hidden-file-input');
        if (input) input.click();
    }

    // Drag and Drop Handling
    function initDragAndDrop() {
        let dragOverlay = document.getElementById('pw-drag-overlay');
        if (!dragOverlay) {
            dragOverlay = document.createElement('div');
            dragOverlay.id = 'pw-drag-overlay';
            dragOverlay.innerHTML = `
                <div class="pw-drag-content">
                    <div class="pw-drag-icon">📥</div>
                    <div class="pw-drag-title">Drop Image or RAW File Here</div>
                    <div class="pw-drag-desc">Supports JPEG, PNG, TIFF, CR2, CR3, NEF, ARW, DNG, RAF, RW2, and 100+ formats</div>
                </div>
            `;
            document.body.appendChild(dragOverlay);
        }

        let dragCounter = 0;

        window.addEventListener('dragenter', (e) => {
            e.preventDefault();
            dragCounter++;
            dragOverlay.classList.add('visible');
        });

        window.addEventListener('dragover', (e) => {
            e.preventDefault();
        });

        window.addEventListener('dragleave', (e) => {
            e.preventDefault();
            dragCounter--;
            if (dragCounter <= 0) {
                dragCounter = 0;
                dragOverlay.classList.remove('visible');
            }
        });

        window.addEventListener('drop', (e) => {
            e.preventDefault();
            dragCounter = 0;
            dragOverlay.classList.remove('visible');
            if (e.dataTransfer && e.dataTransfer.files && e.dataTransfer.files.length > 0) {
                const file = e.dataTransfer.files[0];
                processFile(file, file.name);
            }
        });
    }

    // Load sample image
    async function loadSample() {
        try {
            if (window.pwExifBridge.onStatusUpdate) {
                window.pwExifBridge.onStatusUpdate('Loading sample Grandview Trail image...', true);
            }
            let base = document.baseURI || window.location.href;
            let sampleUrl = new URL('Samples/1510-Rama-Shrine-from-the-Grandview-Trail.jpg?v=' + Date.now(), base).href;
            const resp = await fetch(sampleUrl);
            if (!resp.ok) throw new Error(`HTTP ${resp.status} fetching sample image`);
            const blob = await resp.blob();
            const sampleName = '1510-Rama-Shrine-from-the-Grandview-Trail.jpg';
            const file = (typeof File !== 'undefined')
                ? new File([blob], sampleName, { type: 'image/jpeg' })
                : blob;
            await processFile(file, sampleName, blob.size);
        } catch (e) {
            console.error('[PwExifBridge] Failed to load sample:', e);
            notifyError('Could not load sample image: ' + e.message, 'sample.jpg');
        }
    }

    // Load from Base64 (from C# Photino or App startup)
    async function loadFromBase64(base64Data, fileName) {
        try {
            const binaryStr = atob(base64Data);
            const len = binaryStr.length;
            const bytes = new Uint8Array(len);
            for (let i = 0; i < len; i++) {
                bytes[i] = binaryStr.charCodeAt(i);
            }
            const effectiveName = fileName || 'image.jpg';
            const isStandard = /\.(jpe?g|png|webp|gif|bmp|svg)$/i.test(effectiveName);
            const file = (typeof File !== 'undefined')
                ? new File([bytes], effectiveName, { type: isStandard ? 'image/jpeg' : 'application/octet-stream' })
                : new Blob([bytes]);
            if (isStandard) {
                file._base64Preview = base64Data;
            }
            await processFile(file, effectiveName, len);
        } catch (e) {
            console.error('[PwExifBridge] loadFromBase64 error:', e);
            notifyError('Failed to decode image data: ' + e.message, fileName);
        }
    }

    // Display image preview safely in DOM container
    function displayPreview(containerElement, previewUrl) {
        try {
            const container = containerElement || activePreviewContainer;

            const isInvalidPreview = !previewUrl ||
                previewUrl.startsWith('data:image/arw') ||
                previewUrl.startsWith('data:image/ARW') ||
                previewUrl.startsWith('data:image/raw') ||
                previewUrl.startsWith('data:image/RAW') ||
                previewUrl.startsWith('data:image/tiff') ||
                previewUrl.startsWith('data:image/TIFF') ||
                previewUrl.startsWith('data:image/x-');

            if (isInvalidPreview) {
                if (container) {
                    const existing = container.querySelector('#pw-preview-img');
                    if (existing) {
                        existing.style.display = 'none';
                        existing.src = '';
                    }
                    const wrapper = container.querySelector('#pw-preview-wrapper');
                    if (wrapper) wrapper.style.display = 'none';
                }
                return;
            }

            if (!container) return;

            const oldWrapper = document.getElementById('pw-preview-wrapper');
            if (oldWrapper && oldWrapper.parentElement !== container) {
                try { oldWrapper.remove(); } catch (e) { }
            }

            let wrapper = container.querySelector('#pw-preview-wrapper');
            let img = container.querySelector('#pw-preview-img');

            if (!wrapper) {
                wrapper = document.createElement('div');
                wrapper.id = 'pw-preview-wrapper';
                wrapper.style.cssText = 'position:absolute;top:0;left:0;width:100%;height:100%;display:flex;align-items:center;justify-content:center;overflow:hidden;pointer-events:none;z-index:10;';

                img = document.createElement('img');
                img.id = 'pw-preview-img';
                img.style.cssText = 'max-width:98%;max-height:240px;object-fit:contain;border-radius:6px;box-shadow:0 4px 12px rgba(0,0,0,0.5);display:block;';
                img.alt = 'Image Preview';
                wrapper.appendChild(img);

                if (getComputedStyle(container).position === 'static') {
                    container.style.position = 'relative';
                }
                container.appendChild(wrapper);
            } else if (wrapper.parentElement !== container) {
                container.appendChild(wrapper);
            }

            if (!img) {
                img = wrapper.querySelector('#pw-preview-img');
            }

            if (img) {
                if (img.src === previewUrl) {
                    img.style.display = 'block';
                    if (img.parentElement) img.parentElement.style.display = 'flex';
                    return;
                }
                img.src = previewUrl;
                img.style.display = 'block';
                if (img.parentElement) {
                    img.parentElement.style.display = 'flex';
                }
                img.onload = () => {
                    console.log('[PwExifBridge] Image preview displayed successfully in DOM');
                };
                img.onerror = (e) => {
                    console.warn('[PwExifBridge] Image preview failed to load in DOM. previewUrl:', (previewUrl && previewUrl.length > 80 ? previewUrl.substring(0, 80) + '...' : previewUrl));
                    if (img) img.style.display = 'none';
                    if (img.parentElement) img.parentElement.style.display = 'none';
                };
            }
        } catch (err) {
            console.error('[PwExifBridge] displayPreview error:', err);
        }
    }

    // Public API object
    window.pwExifBridge = {
        init: function () {
            initFileInput();
            initDragAndDrop();
        },
        registerCallback: function (cb) {
            csharpCallback = cb;
        },
        openFileDialog: openFileDialog,
        loadSample: loadSample,
        loadFromBase64: loadFromBase64,
        processFile: processFile,
        openOpenStreetMap: openOpenStreetMap,
        openPeakFinder: openPeakFinder,
        renderMap: renderMap,
        hideMap: hideMap,
        invalidateMapSize: invalidateMapSize,
        ensureMapContainer: ensureMapContainer,
        setContainers: setContainers,
        displayPreview: displayPreview,
        getCurrentMetadataJson: function () {
            return JSON.stringify(currentMetadata || {});
        },
        onStatusUpdate: null
    };

    // Auto-init DOM handlers
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => window.pwExifBridge.init());
    } else {
        window.pwExifBridge.init();
    }
})();
