// PeakFinder-Styled Hardware-Accelerated 360° Continuous Horizon Renderer for PointedHorizons
(function () {
    let currentInstance = null;

    // Public API to update horizon azimuth, FOV or tilt without re-calculating or re-sending JSON
    window.setHorizonParams = function (newAz, newFov, newTilt) {
        if (currentInstance && currentInstance.setParams) {
            currentInstance.setParams(newAz, newFov, newTilt);
        }
    };

    window.initHorizonCanvas = function (container, jsonString, onPanCallback, onFovCallback, onPeakSelectedCallback) {
        if (!container) return;

        let data;
        try {
            data = typeof jsonString === 'string' ? JSON.parse(jsonString) : jsonString;
        } catch (e) {
            console.error('Invalid horizon scene JSON', e);
            return;
        }

        if (!data) return;

        if (currentInstance && typeof currentInstance.cleanup === 'function') {
            try { currentInstance.cleanup(); } catch (e) { }
            currentInstance = null;
        }

        container.innerHTML = '';
        container.style.position = 'relative';
        container.style.width = '100%';
        container.style.height = '100%';
        container.style.overflow = 'hidden';
        container.style.userSelect = 'none';
        container.style.webkitUserSelect = 'none';
        container.style.touchAction = 'none';

        const canvas = document.createElement('canvas');
        canvas.style.display = 'block';
        canvas.style.width = '100%';
        canvas.style.height = '100%';
        canvas.style.cursor = 'grab';
        canvas.style.touchAction = 'none';
        container.appendChild(canvas);

        const ctx = canvas.getContext('2d', { alpha: false });

        // Tooltip element for summits and terrain hover
        let tooltip = document.getElementById('pointedHorizonsTooltip');
        if (!tooltip) {
            tooltip = document.createElement('div');
            tooltip.id = 'pointedHorizonsTooltip';
            tooltip.style.position = 'fixed';
            tooltip.style.display = 'none';
            tooltip.style.pointerEvents = 'none';
            tooltip.style.zIndex = '999999';
            tooltip.style.padding = '8px 12px';
            tooltip.style.borderRadius = '8px';
            tooltip.style.fontSize = '12px';
            tooltip.style.fontFamily = 'Inter, -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif';
            tooltip.style.boxShadow = '0 10px 25px -5px rgba(0, 0, 0, 0.25), 0 8px 10px -6px rgba(0, 0, 0, 0.15)';
            tooltip.style.lineHeight = '1.4';
            document.body.appendChild(tooltip);
        }

        let isDark = data.isDark ?? data.IsDark ?? false;
        let isMetric = data.isMetric ?? data.IsMetric ?? true;

        // 360° Viewing State
        let centerAz = data.azimuthCenter ?? data.AzimuthCenter ?? 0;
        let fov = data.fieldOfView ?? data.FieldOfView ?? 45;
        let verticalTiltDeg = 0; // Dynamic pitch offset in degrees

        let mouseX = -1;
        let mouseY = -1;
        let isDragging = false;
        let dragStartX = 0;
        let dragStartY = 0;
        let dragStartAz = centerAz;
        let dragStartTilt = 0;

        function normalizeAngle(a) {
            a = a % 360;
            if (a < 0) a += 360;
            return a;
        }

        function getCardinal(deg) {
            const directions = ['N', 'NNE', 'NE', 'ENE', 'E', 'ESE', 'SE', 'SSE', 'S', 'SSW', 'SW', 'WSW', 'W', 'WNW', 'NW', 'NNW'];
            const idx = Math.round(deg / 22.5) % 16;
            return directions[idx];
        }

        function drawRoundRect(c, x, y, w, h, r) {
            if (typeof c.roundRect === 'function') {
                c.beginPath();
                c.roundRect(x, y, w, h, r);
                c.fill();
                c.stroke();
            } else {
                c.beginPath();
                c.moveTo(x + r, y);
                c.lineTo(x + w - r, y);
                c.quadraticCurveTo(x + w, y, x + w, y + r);
                c.lineTo(x + w, y + h - r);
                c.quadraticCurveTo(x + w, y + h, x + w - r, y + h);
                c.lineTo(x + r, y + h);
                c.quadraticCurveTo(x, y + h, x, y + h - r);
                c.lineTo(x, y + r);
                c.quadraticCurveTo(x, y, x + r, y);
                c.closePath();
                c.fill();
                c.stroke();
            }
        }

        // Interpolates altitude angle from the 360° dataset for any continuous azimuth
        // Wraps around 0° / 360° seamlessly with zero artifacts
        function getSampleAtAz(pts, az) {
            if (!pts || pts.length === 0) return -15.0;
            const normAz = normalizeAngle(az);
            const n = pts.length;
            const fIdx = (normAz / 360.0) * n;
            const i0 = Math.floor(fIdx) % n;
            const i1 = (i0 + 1) % n;
            const frac = fIdx - Math.floor(fIdx);
            const alt0 = pts[i0].alt ?? pts[i0].altitudeAngle ?? pts[i0].AltitudeAngle ?? -15.0;
            const alt1 = pts[i1].alt ?? pts[i1].altitudeAngle ?? pts[i1].AltitudeAngle ?? -15.0;
            return alt0 * (1.0 - frac) + alt1 * frac;
        }

        function getDistanceAtAz(pts, az) {
            if (!pts || pts.length === 0) return 10.0;
            const normAz = normalizeAngle(az);
            const n = pts.length;
            const fIdx = Math.floor((normAz / 360.0) * n) % n;
            return pts[fIdx].dist ?? pts[fIdx].distanceKm ?? pts[fIdx].DistanceKm ?? 10.0;
        }

        function getElevationAtAz(pts, az) {
            if (!pts || pts.length === 0) return 0;
            const normAz = normalizeAngle(az);
            const n = pts.length;
            const fIdx = Math.floor((normAz / 360.0) * n) % n;
            return pts[fIdx].elev ?? pts[fIdx].elevationM ?? pts[fIdx].ElevationM ?? 0;
        }

        // Floating UI Overlay
        const uiOverlay = document.createElement('div');
        uiOverlay.style.position = 'absolute';
        uiOverlay.style.top = '0';
        uiOverlay.style.left = '0';
        uiOverlay.style.width = '100%';
        uiOverlay.style.height = '100%';
        uiOverlay.style.pointerEvents = 'none';
        uiOverlay.style.touchAction = 'none';
        uiOverlay.style.zIndex = '10';
        container.appendChild(uiOverlay);

        // 2. Vertical Tilt Chevrons (Center Left)
        const tiltContainer = document.createElement('div');
        tiltContainer.style.position = 'absolute';
        tiltContainer.style.left = '16px';
        tiltContainer.style.top = '50%';
        tiltContainer.style.transform = 'translateY(-50%)';
        tiltContainer.style.display = 'flex';
        tiltContainer.style.flexDirection = 'column';
        tiltContainer.style.borderRadius = '18px';
        tiltContainer.style.backgroundColor = isDark ? 'rgba(30, 41, 59, 0.95)' : 'rgba(255, 255, 255, 0.96)';
        tiltContainer.style.boxShadow = '0 4px 14px rgba(0, 0, 0, 0.12)';
        tiltContainer.style.border = isDark ? '1px solid rgba(255, 255, 255, 0.1)' : '1px solid rgba(0, 0, 0, 0.08)';
        tiltContainer.style.overflow = 'hidden';
        tiltContainer.style.pointerEvents = 'auto';

        const btnTiltUp = document.createElement('button');
        btnTiltUp.style.width = '36px';
        btnTiltUp.style.height = '34px';
        btnTiltUp.style.border = 'none';
        btnTiltUp.style.background = 'transparent';
        btnTiltUp.style.cursor = 'pointer';
        btnTiltUp.style.display = 'flex';
        btnTiltUp.style.alignItems = 'center';
        btnTiltUp.style.justifyContent = 'center';
        btnTiltUp.style.color = isDark ? '#F8FAFC' : '#1E293B';
        btnTiltUp.innerHTML = `<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><polyline points="18 15 12 9 6 15"></polyline></svg>`;
        btnTiltUp.title = 'Tilt Horizon Up';
        btnTiltUp.addEventListener('click', () => {
            verticalTiltDeg += 1.5;
            render();
        });

        const btnTiltDown = document.createElement('button');
        btnTiltDown.style.width = '36px';
        btnTiltDown.style.height = '34px';
        btnTiltDown.style.border = 'none';
        btnTiltDown.style.background = 'transparent';
        btnTiltDown.style.cursor = 'pointer';
        btnTiltDown.style.display = 'flex';
        btnTiltDown.style.alignItems = 'center';
        btnTiltDown.style.justifyContent = 'center';
        btnTiltDown.style.color = isDark ? '#F8FAFC' : '#1E293B';
        btnTiltDown.innerHTML = `<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><polyline points="6 9 12 15 18 9"></polyline></svg>`;
        btnTiltDown.title = 'Tilt Horizon Down';
        btnTiltDown.addEventListener('click', () => {
            verticalTiltDeg -= 1.5;
            render();
        });

        tiltContainer.appendChild(btnTiltUp);
        tiltContainer.appendChild(btnTiltDown);
        uiOverlay.appendChild(tiltContainer);

        // 3. Reset Pitch Button (Bottom Right)
        const btnTarget = document.createElement('button');
        btnTarget.style.position = 'absolute';
        btnTarget.style.bottom = '16px';
        btnTarget.style.right = '20px';
        btnTarget.style.width = '38px';
        btnTarget.style.height = '38px';
        btnTarget.style.borderRadius = '50%';
        btnTarget.style.backgroundColor = isDark ? 'rgba(30, 41, 59, 0.95)' : 'rgba(255, 255, 255, 0.96)';
        btnTarget.style.boxShadow = '0 4px 14px rgba(0, 0, 0, 0.12)';
        btnTarget.style.border = isDark ? '1px solid rgba(255, 255, 255, 0.1)' : '1px solid rgba(0, 0, 0, 0.08)';
        btnTarget.style.display = 'flex';
        btnTarget.style.alignItems = 'center';
        btnTarget.style.justifyContent = 'center';
        btnTarget.style.cursor = 'pointer';
        btnTarget.style.pointerEvents = 'auto';
        btnTarget.style.color = isDark ? '#E2E8F0' : '#334155';
        btnTarget.innerHTML = `
            <svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <circle cx="12" cy="12" r="10"></circle>
                <line x1="22" y1="12" x2="18" y2="12"></line>
                <line x1="6" y1="12" x2="2" y2="12"></line>
                <line x1="12" y1="6" x2="12" y2="2"></line>
                <line x1="12" y1="22" x2="12" y2="18"></line>
            </svg>
        `;
        btnTarget.title = 'Reset Pitch to Level';
        btnTarget.addEventListener('click', () => {
            verticalTiltDeg = 0;
            render();
        });
        uiOverlay.appendChild(btnTarget);

        function render() {
            try {
                const rect = container.getBoundingClientRect();
                const width = Math.max(300, rect.width || 300);
                const height = Math.max(200, rect.height || 200);

                const dpr = window.devicePixelRatio || 1;
                canvas.width = Math.round(width * dpr);
                canvas.height = Math.round(height * dpr);
                ctx.resetTransform();
                ctx.scale(dpr, dpr);

                const profile360 = data.profilePoints || data.ProfilePoints || [];
                const peaks360 = data.peaks || data.Peaks || [];
                const ridgeLayers = data.ridgeLayers || data.RidgeLayers || [];

                const rawMinAlt = data.minAltitude != null ? data.minAltitude : (data.MinAltitude != null ? data.MinAltitude : -4.0);
                const rawMaxAlt = data.maxAltitude != null ? data.maxAltitude : (data.MaxAltitude != null ? data.MaxAltitude : 12.0);

                const minAlt = rawMinAlt + verticalTiltDeg;
                const maxAlt = rawMaxAlt + verticalTiltDeg;
                const altSpan = Math.max(2.0, maxAlt - minAlt);

                const startAz = centerAz - fov / 2.0;

                // Theme Palette (PeakFinder Light Paper default)
                const paperBg = isDark ? '#0A0E17' : '#FFFFFF';
                const skylineStroke = isDark ? '#F8FAFC' : '#18181B';
                const ridgeStrokePrimary = isDark ? '#CBD5E1' : '#27272A';
                const ridgeStrokeSecondary = isDark ? '#94A3B8' : '#52525B';
                const slopeContourStroke = isDark ? 'rgba(255, 255, 255, 0.12)' : 'rgba(0, 0, 0, 0.15)';
                const zeroLineColor = isDark ? 'rgba(56, 189, 248, 0.25)' : 'rgba(0, 0, 0, 0.12)';
                const textPrimary = isDark ? '#F8FAFC' : '#18181B';
                const textMuted = isDark ? '#94A3B8' : '#71717A';

                // 1. Draw Paper Background
                ctx.fillStyle = paperBg;
                ctx.fillRect(0, 0, width, height);

                // Helper: Altitude angle to Y pixel coordinate (using full canvas height down to border)
                function altToY(alt) {
                    if (alt == null || isNaN(alt)) return height - 16;
                    const usableTop = 24;
                    const usableBottom = height - 16;
                    const norm = (alt - minAlt) / altSpan;
                    return usableBottom - norm * (usableBottom - usableTop);
                }

                // Helper: Peak Azimuth to X pixel coordinate within current FOV
                function azToX(peakAz) {
                    let diff = peakAz - startAz;
                    while (diff < 0) diff += 360;
                    while (diff >= 360) diff -= 360;
                    if (diff > fov) return null; // Outside current visible window
                    return (diff / fov) * width;
                }

                // Helper: Azimuth to Screen X coordinate centered on centerAz (smooth across screen)
                function azToScreenX(az) {
                    let diff = az - centerAz;
                    while (diff < -180) diff += 360;
                    while (diff > 180) diff -= 360;
                    return (width / 2) + (diff / fov) * width;
                }

                const celestial = data.celestial || data.Celestial || null;

                // 2. Draw 0° Horizontal Eye-Level Reference Line
                if (minAlt <= 0 && maxAlt >= 0) {
                    const zeroY = altToY(0);
                    ctx.save();
                    ctx.strokeStyle = zeroLineColor;
                    ctx.lineWidth = 1;
                    ctx.setLineDash([3, 5]);
                    ctx.beginPath();
                    ctx.moveTo(0, zeroY);
                    ctx.lineTo(width, zeroY);
                    ctx.stroke();

                    ctx.fillStyle = zeroLineColor;
                    ctx.font = '10px Inter, -apple-system, sans-serif';
                    ctx.fillText('0° (eye-level)', 12, zeroY - 4);
                    ctx.restore();
                }

                // 2b. Draw Celestial Arcs (Sun in Yellow, Moon in Blue) & Current Bodies (Sky Layer)
                if (celestial) {
                    function drawCelestialArc(arcPoints, strokeColor, lineWidth, lineDash) {
                        if (!arcPoints || arcPoints.length < 2) return;
                        ctx.save();
                        ctx.strokeStyle = strokeColor;
                        ctx.lineWidth = lineWidth;
                        if (lineDash) ctx.setLineDash(lineDash);
                        ctx.beginPath();
                        let started = false;
                        let prevX = null;

                        for (let i = 0; i < arcPoints.length; i++) {
                            const pt = arcPoints[i];
                            const az = pt.az ?? pt.Azimuth ?? 0;
                            const alt = pt.alt ?? pt.Altitude ?? 0;
                            const x = azToScreenX(az);
                            const y = altToY(alt);

                            if (prevX !== null && Math.abs(x - prevX) > width * 0.7) {
                                started = false;
                            }

                            if (x >= -40 && x <= width + 40 && y >= -150 && y <= height + 150) {
                                if (!started) {
                                    ctx.moveTo(x, y);
                                    started = true;
                                } else {
                                    ctx.lineTo(x, y);
                                }
                            } else {
                                started = false;
                            }
                            prevX = x;
                        }
                        ctx.stroke();
                        ctx.restore();
                    }

                    // 1. Sun Arc (Yellow)
                    const sunStroke = isDark ? '#FBBF24' : '#F59E0B';
                    drawCelestialArc(celestial.sunArc || celestial.SunArc, sunStroke, 2.0, null);

                    // 2. Moon Arc (Blue)
                    const moonStroke = isDark ? '#38BDF8' : '#0284C7';
                    drawCelestialArc(celestial.moonArc || celestial.MoonArc, moonStroke, 1.8, [6, 4]);

                    // 3. Current Sun marker if in FOV and above baseline
                    const curSun = celestial.currentSun || celestial.CurrentSun;
                    if (curSun) {
                        const csAz = curSun.az ?? curSun.Azimuth ?? 0;
                        const csAlt = curSun.alt ?? curSun.Altitude ?? 0;
                        const csX = azToScreenX(csAz);
                        const csY = altToY(csAlt);
                        if (csX >= -10 && csX <= width + 10 && csY >= -20 && csY <= height + 50) {
                            ctx.save();
                            const sunGlow = ctx.createRadialGradient(csX, csY, 2, csX, csY, 14);
                            sunGlow.addColorStop(0, 'rgba(251, 191, 36, 0.9)');
                            sunGlow.addColorStop(0.5, 'rgba(245, 158, 11, 0.4)');
                            sunGlow.addColorStop(1, 'rgba(245, 158, 11, 0.0)');
                            ctx.fillStyle = sunGlow;
                            ctx.beginPath();
                            ctx.arc(csX, csY, 14, 0, Math.PI * 2);
                            ctx.fill();

                            ctx.fillStyle = '#F59E0B';
                            ctx.beginPath();
                            ctx.arc(csX, csY, 5.5, 0, Math.PI * 2);
                            ctx.fill();
                            ctx.strokeStyle = '#FFFFFF';
                            ctx.lineWidth = 1.5;
                            ctx.stroke();

                            ctx.font = 'bold 9px Inter, -apple-system, sans-serif';
                            ctx.fillStyle = isDark ? '#FDE68A' : '#D97706';
                            ctx.textAlign = 'center';
                            ctx.fillText('☀️ Sun', csX, csY - 10);
                            ctx.restore();
                        }
                    }

                    // 4. Current Moon marker if in FOV
                    const curMoon = celestial.currentMoon || celestial.CurrentMoon;
                    if (curMoon) {
                        const cmAz = curMoon.az ?? curMoon.Azimuth ?? 0;
                        const cmAlt = curMoon.alt ?? curMoon.Altitude ?? 0;
                        const cmX = azToScreenX(cmAz);
                        const cmY = altToY(cmAlt);
                        if (cmX >= -10 && cmX <= width + 10 && cmY >= -20 && cmY <= height + 50) {
                            ctx.save();
                            const moonGlow = ctx.createRadialGradient(cmX, cmY, 2, cmX, cmY, 12);
                            moonGlow.addColorStop(0, 'rgba(56, 189, 248, 0.85)');
                            moonGlow.addColorStop(0.5, 'rgba(2, 132, 199, 0.35)');
                            moonGlow.addColorStop(1, 'rgba(2, 132, 199, 0.0)');
                            ctx.fillStyle = moonGlow;
                            ctx.beginPath();
                            ctx.arc(cmX, cmY, 12, 0, Math.PI * 2);
                            ctx.fill();

                            ctx.fillStyle = isDark ? '#38BDF8' : '#0284C7';
                            ctx.beginPath();
                            ctx.arc(cmX, cmY, 5, 0, Math.PI * 2);
                            ctx.fill();
                            ctx.strokeStyle = '#FFFFFF';
                            ctx.lineWidth = 1.5;
                            ctx.stroke();

                            ctx.font = 'bold 9px Inter, -apple-system, sans-serif';
                            ctx.fillStyle = isDark ? '#BAE6FD' : '#0284C7';
                            ctx.textAlign = 'center';
                            ctx.fillText('🌙 Moon', cmX, cmY - 10);
                            ctx.restore();
                        }
                    }
                }

                // Resolution of continuous slice rendering across screen
                const numSteps = Math.min(Math.round(width), 1000);

                // 3. Draw Depth-Shaded Ridge Layers (Furthest to Nearest)
                // Solid paper fill behind closer ridges occludes background layers
                if (ridgeLayers.length > 0) {
                    for (let lIdx = ridgeLayers.length - 1; lIdx >= 0; lIdx--) {
                        const lPts = ridgeLayers[lIdx].points || ridgeLayers[lIdx].Points || [];
                        if (lPts.length < 2) continue;

                        const isClose = lIdx <= 2;
                        const isMid = lIdx > 2 && lIdx <= 5;
                        const strokeColor = isClose ? ridgeStrokePrimary : (isMid ? ridgeStrokeSecondary : (isDark ? '#64748B' : '#71717A'));
                        const lineW = isClose ? 1.5 : (isMid ? 1.2 : 1.0);

                        ctx.save();

                        // A. Solid Occlusion Fill
                        ctx.fillStyle = paperBg;
                        ctx.beginPath();
                        ctx.moveTo(0, height + 10);
                        for (let s = 0; s <= numSteps; s++) {
                            const x = (s / numSteps) * width;
                            const az = normalizeAngle(startAz + (s / numSteps) * fov);
                            const alt = getSampleAtAz(lPts, az);
                            const y = altToY(alt);
                            ctx.lineTo(x, y);
                        }
                        ctx.lineTo(width, height + 10);
                        ctx.closePath();
                        ctx.fill();

                        // B. Faint Topographical Slope Contours (PeakFinder etching style)
                        if (isClose || isMid) {
                            ctx.save();
                            ctx.strokeStyle = slopeContourStroke;
                            ctx.lineWidth = 0.75;
                            for (let c = 1; c <= 2; c++) {
                                ctx.beginPath();
                                let started = false;
                                for (let s = 0; s <= numSteps; s += 2) {
                                    const x = (s / numSteps) * width;
                                    const az = normalizeAngle(startAz + (s / numSteps) * fov);
                                    const baseAlt = getSampleAtAz(lPts, az);
                                    if (baseAlt <= minAlt - 1.0) continue;

                                    const contourAlt = baseAlt - (c * 0.45 * (1.0 + 0.12 * Math.sin(s * 0.08 + c * 2.0)));
                                    const y = altToY(contourAlt);

                                    if (!started) {
                                        ctx.moveTo(x, y);
                                        started = true;
                                    } else {
                                        ctx.lineTo(x, y);
                                    }
                                }
                                ctx.stroke();
                            }
                            ctx.restore();
                        }

                        // C. Crisp Top Ridge Outline Stroke
                        ctx.strokeStyle = strokeColor;
                        ctx.lineWidth = lineW;
                        ctx.lineJoin = 'round';
                        ctx.lineCap = 'round';
                        ctx.beginPath();
                        for (let s = 0; s <= numSteps; s++) {
                            const x = (s / numSteps) * width;
                            const az = normalizeAngle(startAz + (s / numSteps) * fov);
                            const alt = getSampleAtAz(lPts, az);
                            const y = altToY(alt);
                            if (s === 0) ctx.moveTo(x, y);
                            else ctx.lineTo(x, y);
                        }
                        ctx.stroke();

                        ctx.restore();
                    }
                }

                // 4. Draw Main Horizon Skyline Silhouette across visible FOV
                if (profile360.length > 1) {
                    ctx.save();
                    ctx.strokeStyle = skylineStroke;
                    ctx.lineWidth = 1.8;
                    ctx.lineJoin = 'round';
                    ctx.lineCap = 'round';

                    ctx.beginPath();
                    for (let s = 0; s <= numSteps; s++) {
                        const x = (s / numSteps) * width;
                        const az = normalizeAngle(startAz + (s / numSteps) * fov);
                        const alt = getSampleAtAz(profile360, az);
                        const y = altToY(alt);
                        if (s === 0) ctx.moveTo(x, y);
                        else ctx.lineTo(x, y);
                    }
                    ctx.stroke();
                    ctx.restore();
                }

                // 5. Draw Prominent Peaks visible within current FOV
                if (peaks360.length > 0) {
                    const labelLevels = [42, 64, 86];
                    let visiblePeakIdx = 0;

                    peaks360.forEach((peak) => {
                        const peakAz = peak.azimuth ?? peak.Azimuth ?? 0;
                        const px = azToX(peakAz);

                        // Only render if peak is within the current visible FOV
                        if (px === null || px < -30 || px > width + 30) return;

                        const peakAlt = peak.altitudeAngle ?? peak.AltitudeAngle ?? 0;
                        const peakName = peak.name ?? peak.Name ?? 'Summit';
                        const peakElevM = peak.elevationM ?? peak.ElevationM ?? 0;
                        const peakElevFt = peak.elevationFt ?? peak.ElevationFt ?? (peakElevM * 3.28084);
                        const peakDistKm = peak.distanceKm ?? peak.DistanceKm ?? 0;
                        const peakDistMi = peak.distanceMi ?? peak.DistanceMi ?? (peakDistKm * 0.621371);

                        const py = altToY(peakAlt);
                        const labelY = labelLevels[visiblePeakIdx % labelLevels.length];
                        visiblePeakIdx++;

                        ctx.save();
                        // Vertical dashed drop-line from label to summit marker
                        ctx.strokeStyle = isDark ? 'rgba(255, 255, 255, 0.45)' : 'rgba(24, 24, 27, 0.4)';
                        ctx.lineWidth = 1;
                        ctx.setLineDash([2, 3]);
                        ctx.beginPath();
                        ctx.moveTo(px, labelY + 12);
                        ctx.lineTo(px, py);
                        ctx.stroke();
                        ctx.setLineDash([]);

                        // Summit marker dot
                        ctx.fillStyle = isDark ? '#38BDF8' : '#0284C7';
                        ctx.strokeStyle = isDark ? '#FFFFFF' : '#18181B';
                        ctx.lineWidth = 1.5;
                        ctx.beginPath();
                        ctx.arc(px, py, 3.5, 0, Math.PI * 2);
                        ctx.fill();
                        ctx.stroke();

                        // Peak name label text
                        const elevStr = isMetric
                            ? `${Math.round(peakElevM)} m`
                            : `${Math.round(peakElevFt)} ft`;
                        const distStr = isMetric
                            ? `${peakDistKm.toFixed(1)} km`
                            : `${peakDistMi.toFixed(1)} mi`;

                        ctx.font = 'bold 11px Inter, -apple-system, sans-serif';
                        ctx.textAlign = 'center';
                        const nameMetrics = ctx.measureText(peakName);
                        const badgeW = Math.max(nameMetrics.width + 14, 64);

                        // Pill background for peak label
                        ctx.fillStyle = isDark ? 'rgba(30, 41, 59, 0.95)' : 'rgba(255, 255, 255, 0.95)';
                        ctx.strokeStyle = isDark ? 'rgba(255, 255, 255, 0.15)' : 'rgba(0, 0, 0, 0.12)';
                        ctx.lineWidth = 1;
                        drawRoundRect(ctx, px - badgeW / 2, labelY - 14, badgeW, 26, 6);

                        // Text: Peak Name
                        ctx.fillStyle = textPrimary;
                        ctx.fillText(peakName, px, labelY - 2);

                        // Text: Elevation / Distance subtitle
                        ctx.font = '9px Inter, -apple-system, sans-serif';
                        ctx.fillStyle = textMuted;
                        ctx.fillText(`${elevStr} · ${distStr}`, px, labelY + 9);

                        ctx.restore();
                    });
                }

                // 5b. Draw Topographic Rise & Set Intersection Badges (Sun in Yellow, Moon in Blue)
                if (celestial) {
                    const cEvents = [
                        { key: 'sunrise', evt: celestial.sunrise || celestial.Sunrise, label: 'Sunrise', icon: '☀️', isSun: true },
                        { key: 'sunset', evt: celestial.sunset || celestial.Sunset, label: 'Sunset', icon: '☀️', isSun: true },
                        { key: 'moonrise', evt: celestial.moonrise || celestial.Moonrise, label: 'Moonrise', icon: '🌙', isSun: false },
                        { key: 'moonset', evt: celestial.moonset || celestial.Moonset, label: 'Moonset', icon: '🌙', isSun: false }
                    ];

                    cEvents.forEach((item) => {
                        if (!item.evt) return;
                        const az = item.evt.az ?? item.evt.Azimuth;
                        const alt = item.evt.alt ?? item.evt.Altitude;
                        const timeStr = item.evt.time ?? item.evt.Time ?? '';
                        if (az == null || alt == null) return;

                        const px = azToScreenX(az);
                        // Check if within visible FOV bounds
                        if (px < 15 || px > width - 15) return;

                        const py = altToY(alt);
                        const isSun = item.isSun;
                        const eventColor = isSun
                            ? (isDark ? '#FBBF24' : '#F59E0B')
                            : (isDark ? '#38BDF8' : '#0284C7');
                        const eventBorder = isSun
                            ? (isDark ? '#F59E0B' : '#D97706')
                            : (isDark ? '#0284C7' : '#0369A1');

                        ctx.save();

                        // A. Marker pin on horizon ridge intersection
                        ctx.strokeStyle = eventColor;
                        ctx.lineWidth = 1.5;
                        ctx.beginPath();
                        ctx.arc(px, py, 6.5, 0, Math.PI * 2);
                        ctx.stroke();

                        ctx.fillStyle = eventColor;
                        ctx.beginPath();
                        ctx.arc(px, py, 3.5, 0, Math.PI * 2);
                        ctx.fill();
                        ctx.strokeStyle = '#FFFFFF';
                        ctx.lineWidth = 1;
                        ctx.stroke();

                        // B. Vertical dashed indicator line
                        const badgeH = 28;
                        const badgeY = Math.max(26, Math.min(height - 40, py - 42));

                        ctx.strokeStyle = eventColor;
                        ctx.lineWidth = 1;
                        ctx.setLineDash([2, 3]);
                        ctx.beginPath();
                        ctx.moveTo(px, py > badgeY ? py - 7 : py + 7);
                        ctx.lineTo(px, py > badgeY ? badgeY + badgeH : badgeY);
                        ctx.stroke();
                        ctx.setLineDash([]);

                        // C. Pill badge container
                        const line1 = `${item.icon} ${item.label} ${timeStr}`;
                        const line2 = `Ridge ${alt >= 0 ? '+' : ''}${alt.toFixed(1)}° · ${Math.round(az)}° ${getCardinal(az)}`;

                        ctx.font = 'bold 11px Inter, -apple-system, sans-serif';
                        const textW1 = ctx.measureText(line1).width;
                        ctx.font = '9px Inter, -apple-system, sans-serif';
                        const textW2 = ctx.measureText(line2).width;
                        const badgeW = Math.max(textW1, textW2) + 16;
                        const badgeX = Math.max(8, Math.min(width - badgeW - 8, px - badgeW / 2));

                        // Pill background & border
                        ctx.fillStyle = isDark ? 'rgba(15, 23, 42, 0.95)' : 'rgba(255, 255, 255, 0.96)';
                        ctx.strokeStyle = eventBorder;
                        ctx.lineWidth = 1.5;
                        drawRoundRect(ctx, badgeX, badgeY, badgeW, badgeH, 6);

                        // Pill text: Title
                        ctx.font = 'bold 10px Inter, -apple-system, sans-serif';
                        ctx.fillStyle = eventBorder;
                        ctx.textAlign = 'center';
                        ctx.fillText(line1, badgeX + badgeW / 2, badgeY + 12);

                        // Pill text: Subtitle (topography altitude angle & cardinal)
                        ctx.font = '9px Inter, -apple-system, sans-serif';
                        ctx.fillStyle = textMuted;
                        ctx.fillText(line2, badgeX + badgeW / 2, badgeY + 23);

                        ctx.restore();
                    });
                }

                // 6. Interactive Hover Cursor & Info Tooltip
                if (mouseX >= 0 && mouseX <= width && mouseY >= 0 && mouseY <= height) {
                    const cursorAz = normalizeAngle(startAz + (mouseX / width) * fov);
                    const cursorAlt = getSampleAtAz(profile360, cursorAz);
                    const hx = mouseX;
                    const hy = altToY(cursorAlt);

                    ctx.save();
                    ctx.strokeStyle = isDark ? '#38BDF8' : '#0284C7';
                    ctx.lineWidth = 1.5;
                    ctx.beginPath();
                    ctx.arc(hx, hy, 4.5, 0, Math.PI * 2);
                    ctx.stroke();
                    ctx.restore();

                    // Position tooltip
                    tooltip.style.display = 'block';
                    tooltip.style.backgroundColor = isDark ? 'rgba(15, 23, 42, 0.96)' : 'rgba(255, 255, 255, 0.98)';
                    tooltip.style.color = isDark ? '#F8FAFC' : '#18181B';
                    tooltip.style.border = isDark ? '1px solid rgba(255, 255, 255, 0.15)' : '1px solid rgba(0, 0, 0, 0.12)';
                    tooltip.style.left = `${Math.min(window.innerWidth - 220, rect.left + hx + 12)}px`;
                    tooltip.style.top = `${Math.max(10, rect.top + Math.min(hy, height - 80))}px`;

                    const hpElevM = getElevationAtAz(profile360, cursorAz);
                    const hpDistKm = getDistanceAtAz(profile360, cursorAz);

                    const elevText = isMetric
                        ? `${Math.round(hpElevM).toLocaleString()} m`
                        : `${Math.round(hpElevM * 3.28084).toLocaleString()} ft`;
                    const distText = isMetric
                        ? `${hpDistKm.toFixed(1)} km`
                        : `${(hpDistKm * 0.621371).toFixed(1)} mi`;

                    tooltip.innerHTML = `
                        <div style="font-weight:700; color:${isDark ? '#38BDF8' : '#0284C7'}; margin-bottom:2px;">
                            ${Math.round(cursorAz)}° ${getCardinal(cursorAz)}
                        </div>
                        <div style="font-size:11px; opacity:0.9;">
                            Elev: <b>${elevText}</b> &nbsp;|&nbsp; Dist: <b>${distText}</b>
                        </div>
                        <div style="font-size:11px; opacity:0.75; margin-top:2px;">
                            Altitude Angle: <b>${cursorAlt >= 0 ? '+' : ''}${cursorAlt.toFixed(1)}°</b>
                        </div>
                    `;
                } else {
                    tooltip.style.display = 'none';
                }
            } catch (err) {
                console.error('Horizon canvas render error:', err);
            }
        }

        // -------------------------------------------------------------
        // Mouse Events (Desktop)
        // -------------------------------------------------------------
        function onMouseDown(e) {
            isDragging = true;
            dragStartX = e.clientX;
            dragStartY = e.clientY;
            dragStartAz = centerAz;
            dragStartTilt = verticalTiltDeg;
            canvas.style.cursor = 'grabbing';
        }

        function onMouseMove(e) {
            if (isDragging) {
                const deltaX = e.clientX - dragStartX;
                const deltaY = e.clientY - dragStartY;
                const rect = canvas.getBoundingClientRect();

                // 360° Infinite horizontal drag pan
                const deltaAz = -(deltaX / Math.max(1, rect.width)) * fov;
                centerAz = normalizeAngle(dragStartAz + deltaAz);

                // Vertical pitch tilt
                const deltaTilt = (deltaY / Math.max(1, rect.height)) * 14.0;
                verticalTiltDeg = Math.max(-25, Math.min(25, dragStartTilt + deltaTilt));

                render();

                // Synchronize Directions Bar immediately in the same frame
                if (window.setDirectionsBarParams) {
                    window.setDirectionsBarParams(centerAz, fov);
                }

                if (onPanCallback) {
                    onPanCallback(centerAz);
                }
            } else {
                const rect = canvas.getBoundingClientRect();
                if (e.clientX >= rect.left && e.clientX <= rect.right &&
                    e.clientY >= rect.top && e.clientY <= rect.bottom) {
                    mouseX = e.clientX - rect.left;
                    mouseY = e.clientY - rect.top;
                    render();
                } else {
                    if (mouseX !== -1) {
                        mouseX = -1;
                        mouseY = -1;
                        render();
                    }
                }
            }
        }

        function onMouseUp() {
            if (isDragging) {
                isDragging = false;
                canvas.style.cursor = 'grab';
                if (onPanCallback) {
                    onPanCallback(centerAz);
                }
            }
        }

        function onMouseLeave() {
            if (!isDragging) {
                mouseX = -1;
                mouseY = -1;
                render();
            }
        }

        // Mouse Wheel for Smooth Field Of View Zooming (3° - 160°)
        function onWheel(e) {
            e.preventDefault();
            const delta = e.deltaY > 0 ? 3 : -3;
            fov = Math.max(3, Math.min(160, fov + delta));
            render();

            if (window.setDirectionsBarParams) {
                window.setDirectionsBarParams(centerAz, fov);
            }

            if (onFovCallback) {
                onFovCallback(fov);
            }
        }

        canvas.addEventListener('mousedown', onMouseDown);
        window.addEventListener('mousemove', onMouseMove);
        window.addEventListener('mouseup', onMouseUp);
        canvas.addEventListener('mouseleave', onMouseLeave);
        canvas.addEventListener('wheel', onWheel, { passive: false });

        // -------------------------------------------------------------
        // Touch Events (Mobile Android / Tablets / Touchscreens)
        // Supports:
        // - Single-finger 360° continuous drag pan & pitch tilt
        // - Two-finger smooth pinch-to-zoom (FOV 3° - 160°)
        // - Simultaneous pinch zoom + pan
        // - Tap to inspect summit altitude angle, elevation, & distance
        // -------------------------------------------------------------
        let isTouchDragging = false;
        let isPinching = false;
        let touchStartX = 0;
        let touchStartY = 0;
        let touchStartAz = centerAz;
        let touchStartTilt = verticalTiltDeg;
        let pinchStartDist = 0;
        let pinchStartFov = fov;
        let pinchStartMidX = 0;
        let pinchStartMidY = 0;
        let touchMoved = false;

        function getTouchDist(t0, t1) {
            return Math.hypot(t0.clientX - t1.clientX, t0.clientY - t1.clientY);
        }

        function getTouchMid(t0, t1) {
            return {
                x: (t0.clientX + t1.clientX) / 2,
                y: (t0.clientY + t1.clientY) / 2
            };
        }

        function onTouchStart(e) {
            if (e.touches.length === 1) {
                isTouchDragging = true;
                isPinching = false;
                touchMoved = false;
                touchStartX = e.touches[0].clientX;
                touchStartY = e.touches[0].clientY;
                touchStartAz = centerAz;
                touchStartTilt = verticalTiltDeg;
                tooltip.style.display = 'none';
                mouseX = -1;
                mouseY = -1;
            } else if (e.touches.length >= 2) {
                isPinching = true;
                isTouchDragging = false;
                touchMoved = true;
                const t0 = e.touches[0];
                const t1 = e.touches[1];
                pinchStartDist = getTouchDist(t0, t1);
                pinchStartFov = fov;
                const mid = getTouchMid(t0, t1);
                pinchStartMidX = mid.x;
                pinchStartMidY = mid.y;
                touchStartAz = centerAz;
                touchStartTilt = verticalTiltDeg;
                tooltip.style.display = 'none';
                mouseX = -1;
                mouseY = -1;
            }
            e.preventDefault();
        }

        function onTouchMove(e) {
            if (isPinching && e.touches.length >= 2) {
                const t0 = e.touches[0];
                const t1 = e.touches[1];
                const currentDist = getTouchDist(t0, t1);

                if (pinchStartDist > 0 && currentDist > 0) {
                    const scale = pinchStartDist / currentDist;
                    fov = Math.max(3, Math.min(160, pinchStartFov * scale));
                }

                // Two-finger pan while pinching
                const mid = getTouchMid(t0, t1);
                const deltaMidX = mid.x - pinchStartMidX;
                const deltaMidY = mid.y - pinchStartMidY;
                const rect = canvas.getBoundingClientRect();
                const deltaAz = -(deltaMidX / Math.max(1, rect.width)) * fov;
                centerAz = normalizeAngle(touchStartAz + deltaAz);

                const deltaTilt = (deltaMidY / Math.max(1, rect.height)) * 14.0;
                verticalTiltDeg = Math.max(-25, Math.min(25, touchStartTilt + deltaTilt));

                render();

                if (window.setDirectionsBarParams) {
                    window.setDirectionsBarParams(centerAz, fov);
                }
                if (onFovCallback) {
                    onFovCallback(fov);
                }
                if (onPanCallback) {
                    onPanCallback(centerAz);
                }
                e.preventDefault();
            } else if (isTouchDragging && e.touches.length === 1) {
                const deltaX = e.touches[0].clientX - touchStartX;
                const deltaY = e.touches[0].clientY - touchStartY;

                if (Math.abs(deltaX) > 2 || Math.abs(deltaY) > 2) {
                    touchMoved = true;
                }

                const rect = canvas.getBoundingClientRect();

                // 360° Infinite horizontal drag pan
                const deltaAz = -(deltaX / Math.max(1, rect.width)) * fov;
                centerAz = normalizeAngle(touchStartAz + deltaAz);

                // Vertical pitch tilt
                const deltaTilt = (deltaY / Math.max(1, rect.height)) * 14.0;
                verticalTiltDeg = Math.max(-25, Math.min(25, touchStartTilt + deltaTilt));

                render();

                if (window.setDirectionsBarParams) {
                    window.setDirectionsBarParams(centerAz, fov);
                }
                if (onPanCallback) {
                    onPanCallback(centerAz);
                }
                e.preventDefault();
            }
        }

        function onTouchEnd(e) {
            if (isPinching) {
                if (e.touches.length === 1) {
                    // Transition smoothly to 1-finger drag with remaining finger
                    isPinching = false;
                    isTouchDragging = true;
                    touchStartX = e.touches[0].clientX;
                    touchStartY = e.touches[0].clientY;
                    touchStartAz = centerAz;
                    touchStartTilt = verticalTiltDeg;
                    touchMoved = true;
                } else if (e.touches.length === 0) {
                    isPinching = false;
                    if (onFovCallback) {
                        onFovCallback(fov);
                    }
                    if (onPanCallback) {
                        onPanCallback(centerAz);
                    }
                }
            } else if (isTouchDragging) {
                if (e.touches.length === 0) {
                    isTouchDragging = false;
                    if (onPanCallback) {
                        onPanCallback(centerAz);
                    }

                    // Quick tap without dragging: inspect clicked summit/terrain point
                    if (!touchMoved) {
                        const rect = canvas.getBoundingClientRect();
                        if (touchStartX >= rect.left && touchStartX <= rect.right &&
                            touchStartY >= rect.top && touchStartY <= rect.bottom) {
                            mouseX = touchStartX - rect.left;
                            mouseY = touchStartY - rect.top;
                            render();
                        }
                    }
                }
            }
        }

        function onTouchCancel() {
            isTouchDragging = false;
            isPinching = false;
        }

        canvas.addEventListener('touchstart', onTouchStart, { passive: false });
        window.addEventListener('touchmove', onTouchMove, { passive: false });
        window.addEventListener('touchend', onTouchEnd, { passive: true });
        window.addEventListener('touchcancel', onTouchCancel, { passive: true });

        // ResizeObserver for dynamic responsiveness
        if (window.ResizeObserver && !container._horizonObserver) {
            container._horizonObserver = new ResizeObserver(() => render());
            container._horizonObserver.observe(container);
        }

        function cleanup() {
            canvas.removeEventListener('mousedown', onMouseDown);
            window.removeEventListener('mousemove', onMouseMove);
            window.removeEventListener('mouseup', onMouseUp);
            canvas.removeEventListener('mouseleave', onMouseLeave);
            canvas.removeEventListener('wheel', onWheel);

            canvas.removeEventListener('touchstart', onTouchStart);
            window.removeEventListener('touchmove', onTouchMove);
            window.removeEventListener('touchend', onTouchEnd);
            window.removeEventListener('touchcancel', onTouchCancel);

            if (container._horizonObserver) {
                try { container._horizonObserver.disconnect(); } catch (e) { }
                container._horizonObserver = null;
            }
        }

        // Store instance API so C# can update azimuth, FOV or tilt without re-calculating
        currentInstance = {
            setParams: function (newAz, newFov, newTilt) {
                if (newAz != null) {
                    centerAz = normalizeAngle(newAz);
                }
                if (newFov != null) {
                    fov = Math.max(3, Math.min(160, newFov));
                }
                if (newTilt != null) {
                    verticalTiltDeg = newTilt;
                }
                render();
            },
            cleanup: cleanup
        };

        render();
    };
})();
