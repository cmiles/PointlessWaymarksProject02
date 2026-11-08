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
            tooltip.style.zIndex = '5';
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
                    const curMoon = celestial.currentMoon || celestial.CurrentMoon;

                    let csX = null, csY = null, cmX = null, cmY = null;
                    let showSun = false, showMoon = false;

                    if (curSun) {
                        const csAz = curSun.az ?? curSun.Azimuth ?? 0;
                        const csAlt = curSun.alt ?? curSun.Altitude ?? 0;
                        csX = azToScreenX(csAz);
                        csY = altToY(csAlt);
                        showSun = (csX >= -10 && csX <= width + 10 && csY >= -20 && csY <= height + 50);
                    }
                    if (curMoon) {
                        const cmAz = curMoon.az ?? curMoon.Azimuth ?? 0;
                        const cmAlt = curMoon.alt ?? curMoon.Altitude ?? 0;
                        cmX = azToScreenX(cmAz);
                        cmY = altToY(cmAlt);
                        showMoon = (cmX >= -10 && cmX <= width + 10 && cmY >= -20 && cmY <= height + 50);
                    }

                    const skyOverlap = (showSun && showMoon && Math.hypot(csX - cmX, csY - cmY) < 32);

                    if (showSun) {
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
                        const sunLabelY = skyOverlap ? (csY - 13) : (csY - 10);
                        ctx.fillText('☀️ Sun', csX, sunLabelY);
                        ctx.restore();
                    }

                    // 4. Current Moon marker if in FOV
                    if (showMoon) {
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
                        const moonLabelY = skyOverlap ? (cmY + 20) : (cmY - 10);
                        ctx.fillText('🌙 Moon', cmX, moonLabelY);
                        ctx.restore();
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
                const visiblePeakBadges = [];
                if (peaks360.length > 0) {
                    const rawVisible = [];
                    peaks360.forEach((peak) => {
                        const peakAz = peak.azimuth ?? peak.Azimuth ?? 0;
                        const px = azToX(peakAz);

                        // Only render if peak is within the current visible FOV
                        if (px === null || px < -40 || px > width + 40) return;

                        const peakAlt = peak.altitudeAngle ?? peak.AltitudeAngle ?? 0;
                        const peakName = peak.name ?? peak.Name ?? 'Summit';
                        const peakElevM = peak.elevationM ?? peak.ElevationM ?? 0;
                        const peakElevFt = peak.elevationFt ?? peak.ElevationFt ?? (peakElevM * 3.28084);
                        const peakDistKm = peak.distanceKm ?? peak.DistanceKm ?? 0;
                        const peakDistMi = peak.distanceMi ?? peak.DistanceMi ?? (peakDistKm * 0.621371);
                        const isSkyline = peak.isSkyline ?? peak.IsSkyline ?? true;
                        const py = altToY(peakAlt);

                        const elevStr = isMetric
                            ? `${Math.round(peakElevM)} m`
                            : `${Math.round(peakElevFt)} ft`;
                        const distStr = isMetric
                            ? `${peakDistKm.toFixed(1)} km`
                            : `${peakDistMi.toFixed(1)} mi`;

                        // Remove 'midground' label from display text (clean elev · dist format)
                        const subStr = `${elevStr} · ${distStr}`;

                        // Measure both title and subtitle widths
                        ctx.font = 'bold 11px Inter, -apple-system, sans-serif';
                        const nameW = ctx.measureText(peakName).width;
                        ctx.font = '9px Inter, -apple-system, sans-serif';
                        const subW = ctx.measureText(subStr).width;
                        const maxTextW = Math.max(nameW, subW);

                        // Give text generous margin from border, ensuring border fully contains text
                        const badgeW = Math.max(maxTextW + 20, 68);
                        const badgeH = 29;

                        rawVisible.push({
                            peak,
                            peakAz,
                            px,
                            py,
                            peakName,
                            subStr,
                            isSkyline,
                            badgeW,
                            badgeH
                        });
                    });

                    // Sort peaks deterministically by screen X (left to right)
                    rawVisible.sort((a, b) => a.px - b.px);

                    // Vertical tier levels (top of badge Y coordinate)
                    // Each tier is separated by 35px (giving 6px clear vertical gap between 29px badges)
                    const maxAllowedY = Math.max(65, height - 35);
                    const candidateTiers = [28, 63, 98, 133, 168].filter(y => y + 29 <= maxAllowedY);
                    if (candidateTiers.length === 0) candidateTiers.push(28);

                    const padX = 7;

                    // Place each peak badge avoiding overlaps
                    rawVisible.forEach((p) => {
                        const idealX = Math.max(8, Math.min(width - p.badgeW - 8, p.px - p.badgeW / 2));
                        const minX = Math.max(8, p.px - p.badgeW + 12);
                        const maxX = Math.min(width - p.badgeW - 8, p.px - 12);

                        let bestTier = candidateTiers[0];
                        let bestX = idealX;
                        let bestScore = Infinity;

                        for (let t = 0; t < candidateTiers.length; t++) {
                            const ty = candidateTiers[t];
                            const badgesOnTier = visiblePeakBadges.filter(b => b.badgeY === ty);

                            const collidesWith = (x) => badgesOnTier.some(b =>
                                !(x + p.badgeW + padX <= b.badgeX || b.badgeX + b.badgeW + padX <= x));

                            let tierX = idealX;
                            let hasCollision = collidesWith(tierX);

                            // If ideal center collides on this tier, try shifting slightly left or right
                            if (hasCollision && minX <= maxX) {
                                const colliding = badgesOnTier.filter(b =>
                                    !(idealX + p.badgeW + padX <= b.badgeX || b.badgeX + b.badgeW + padX <= idealX));
                                if (colliding.length > 0) {
                                    const rightmostObstacle = Math.max(...colliding.map(b => b.badgeX + b.badgeW));
                                    const candidateRight = rightmostObstacle + padX;
                                    if (candidateRight <= maxX && !collidesWith(candidateRight)) {
                                        tierX = candidateRight;
                                        hasCollision = false;
                                    } else {
                                        const leftmostObstacle = Math.min(...colliding.map(b => b.badgeX));
                                        const candidateLeft = leftmostObstacle - p.badgeW - padX;
                                        if (candidateLeft >= minX && !collidesWith(candidateLeft)) {
                                            tierX = candidateLeft;
                                            hasCollision = false;
                                        }
                                    }
                                }
                            }

                            // Score candidate tier
                            let score = 0;
                            const shift = Math.abs(tierX - idealX);
                            const isAboveSummit = (ty + p.badgeH <= p.py - 4);

                            if (!hasCollision) {
                                // Clean collision-free fit
                                score = (t * 20) + (shift * 0.5);
                                if (!isAboveSummit) {
                                    score += 300 + Math.abs(ty - p.py);
                                }
                            } else {
                                // Overlap penalty
                                score = 2000 + (t * 50);
                            }

                            if (score < bestScore) {
                                bestScore = score;
                                bestTier = ty;
                                bestX = tierX;
                            }
                        }

                        visiblePeakBadges.push({
                            ...p,
                            badgeX: bestX,
                            badgeY: bestTier,
                            textCenterX: bestX + p.badgeW / 2
                        });
                    });

                    // Pass 1: Draw connector drop-lines and summit marker dots
                    visiblePeakBadges.forEach((b) => {
                        ctx.save();

                        // Vertical dashed drop-line from label badge to summit marker
                        ctx.strokeStyle = b.isSkyline
                            ? (isDark ? 'rgba(255, 255, 255, 0.45)' : 'rgba(24, 24, 27, 0.4)')
                            : (isDark ? 'rgba(52, 211, 153, 0.55)' : 'rgba(5, 150, 105, 0.45)');
                        ctx.lineWidth = 1;
                        ctx.setLineDash([2, 3]);
                        ctx.beginPath();
                        const lineStartY = b.badgeY + b.badgeH <= b.py ? (b.badgeY + b.badgeH) : b.badgeY;
                        ctx.moveTo(b.px, lineStartY);
                        ctx.lineTo(b.px, b.py);
                        ctx.stroke();
                        ctx.setLineDash([]);

                        // Summit marker dot: Skyline = bright blue/cyan; Midground = emerald green
                        ctx.fillStyle = b.isSkyline
                            ? (isDark ? '#38BDF8' : '#0284C7')
                            : (isDark ? '#34D399' : '#059669');
                        ctx.strokeStyle = isDark ? '#FFFFFF' : '#18181B';
                        ctx.lineWidth = 1.5;
                        ctx.beginPath();
                        ctx.arc(b.px, b.py, b.isSkyline ? 3.5 : 3.0, 0, Math.PI * 2);
                        ctx.fill();
                        ctx.stroke();

                        ctx.restore();
                    });

                    // Pass 2: Draw all pill badge containers and text on top
                    visiblePeakBadges.forEach((b) => {
                        ctx.save();

                        // Pill background for peak label (accent border for midground summits)
                        ctx.fillStyle = isDark ? 'rgba(30, 41, 59, 0.95)' : 'rgba(255, 255, 255, 0.95)';
                        ctx.strokeStyle = b.isSkyline
                            ? (isDark ? 'rgba(255, 255, 255, 0.18)' : 'rgba(0, 0, 0, 0.14)')
                            : (isDark ? 'rgba(52, 211, 153, 0.55)' : 'rgba(5, 150, 105, 0.45)');
                        ctx.lineWidth = 1;
                        drawRoundRect(ctx, b.badgeX, b.badgeY, b.badgeW, b.badgeH, 6);

                        // Text: Peak Name (Title)
                        ctx.font = 'bold 11px Inter, -apple-system, sans-serif';
                        ctx.textAlign = 'center';
                        ctx.fillStyle = textPrimary;
                        ctx.fillText(b.peakName, b.textCenterX, b.badgeY + 13);

                        // Text: Elevation / Distance (Subtitle)
                        ctx.font = '9px Inter, -apple-system, sans-serif';
                        ctx.fillStyle = textMuted;
                        ctx.fillText(b.subStr, b.textCenterX, b.badgeY + 24);

                        ctx.restore();
                    });
                }

                // 5b. Draw Topographic Rise & Set Intersection Badges (Sun in Yellow, Moon in Blue)
                // Overlaps avoided via intelligent horizontal nudging and vertical tiering
                if (celestial) {
                    const cEvents = [
                        { key: 'sunrise', evt: celestial.sunrise || celestial.Sunrise, label: 'Sunrise', icon: '☀️', isSun: true },
                        { key: 'sunset', evt: celestial.sunset || celestial.Sunset, label: 'Sunset', icon: '☀️', isSun: true },
                        { key: 'moonrise', evt: celestial.moonrise || celestial.Moonrise, label: 'Moonrise', icon: '🌙', isSun: false },
                        { key: 'moonset', evt: celestial.moonset || celestial.Moonset, label: 'Moonset', icon: '🌙', isSun: false }
                    ];

                    const visibleEvents = [];
                    cEvents.forEach((item) => {
                        if (!item.evt) return;
                        const az = item.evt.az ?? item.evt.Azimuth;
                        const alt = item.evt.alt ?? item.evt.Altitude;
                        const timeStr = item.evt.time ?? item.evt.Time ?? '';
                        if (az == null || alt == null) return;

                        const px = azToScreenX(az);
                        if (px < 15 || px > width - 15) return;

                        const py = altToY(alt);
                        const isSun = item.isSun;
                        const eventColor = isSun
                            ? (isDark ? '#FBBF24' : '#F59E0B')
                            : (isDark ? '#38BDF8' : '#0284C7');
                        const eventBorder = isSun
                            ? (isDark ? '#F59E0B' : '#D97706')
                            : (isDark ? '#0284C7' : '#0369A1');

                        const line1 = `${item.icon} ${item.label} ${timeStr}`;
                        const line2 = `Ridge ${alt >= 0 ? '+' : ''}${alt.toFixed(1)}° · ${Math.round(az)}° ${getCardinal(az)}`;

                        ctx.font = 'bold 11px Inter, -apple-system, sans-serif';
                        const textW1 = ctx.measureText(line1).width;
                        ctx.font = '9px Inter, -apple-system, sans-serif';
                        const textW2 = ctx.measureText(line2).width;
                        const badgeW = Math.max(textW1, textW2) + 16;
                        const badgeH = 28;

                        const badgeX = Math.max(8, Math.min(width - badgeW - 8, px - badgeW / 2));
                        const badgeY = Math.max(26, Math.min(height - 40, py - 42));

                        visibleEvents.push({
                            item,
                            az,
                            alt,
                            timeStr,
                            px,
                            py,
                            isSun,
                            eventColor,
                            eventBorder,
                            line1,
                            line2,
                            badgeW,
                            badgeH,
                            badgeX,
                            badgeY
                        });
                    });

                    // Collision avoidance across badges (e.g. Sunset and Moonset occurring close together)
                    if (visibleEvents.length > 1) {
                        visibleEvents.sort((a, b) => a.px - b.px);

                        const padX = 8;
                        const padY = 6;

                        for (let i = 0; i < visibleEvents.length; i++) {
                            for (let j = i + 1; j < visibleEvents.length; j++) {
                                const b1 = visibleEvents[i];
                                const b2 = visibleEvents[j];

                                const hOverlap = (b1.badgeX < b2.badgeX + b2.badgeW + padX) && (b2.badgeX < b1.badgeX + b1.badgeW + padX);
                                if (!hOverlap) continue;

                                // Attempt horizontal bias separation first (nudge b1 left, b2 right while keeping px within badge)
                                const b1MaxShiftLeft = Math.max(8, b1.px - b1.badgeW + 14);
                                const b2MaxShiftRight = Math.min(width - b2.badgeW - 8, b2.px - 14);

                                if (b1MaxShiftLeft + b1.badgeW + padX <= b2MaxShiftRight) {
                                    b1.badgeX = b1MaxShiftLeft;
                                    b2.badgeX = b2MaxShiftRight;
                                    continue;
                                }

                                // Stagger vertically if still overlapping horizontally
                                const vOverlap = (b1.badgeY < b2.badgeY + b2.badgeH + padY) && (b2.badgeY < b1.badgeY + b1.badgeH + padY);
                                if (vOverlap) {
                                    // Sun remains at standard ridge elevation, Moon tiers above or below
                                    const primary = (b2.isSun && !b1.isSun) ? b2 : b1;
                                    const secondary = (primary === b1) ? b2 : b1;

                                    const stackAboveY = primary.badgeY - secondary.badgeH - padY;
                                    const stackBelowY = primary.badgeY + primary.badgeH + padY;

                                    if (stackAboveY >= 24) {
                                        secondary.badgeY = stackAboveY;
                                    } else if (stackBelowY + secondary.badgeH <= height - 20) {
                                        secondary.badgeY = stackBelowY;
                                    } else {
                                        secondary.badgeY = Math.min(height - 40, secondary.py + 18);
                                    }
                                }
                            }
                        }
                    }

                    // Pass 1: Draw all horizon ridge pins and vertical connector lines
                    visibleEvents.forEach((ev) => {
                        ctx.save();

                        // Marker pin on horizon ridge intersection
                        ctx.strokeStyle = ev.eventColor;
                        ctx.lineWidth = 1.5;
                        ctx.beginPath();
                        ctx.arc(ev.px, ev.py, 6.5, 0, Math.PI * 2);
                        ctx.stroke();

                        ctx.fillStyle = ev.eventColor;
                        ctx.beginPath();
                        ctx.arc(ev.px, ev.py, 3.5, 0, Math.PI * 2);
                        ctx.fill();
                        ctx.strokeStyle = '#FFFFFF';
                        ctx.lineWidth = 1;
                        ctx.stroke();

                        // Vertical dashed indicator line from ridge pin to badge base
                        ctx.strokeStyle = ev.eventColor;
                        ctx.lineWidth = 1;
                        ctx.setLineDash([2, 3]);
                        ctx.beginPath();
                        ctx.moveTo(ev.px, ev.py > ev.badgeY ? ev.py - 7 : ev.py + 7);
                        ctx.lineTo(ev.px, ev.py > ev.badgeY ? ev.badgeY + ev.badgeH : ev.badgeY);
                        ctx.stroke();
                        ctx.setLineDash([]);

                        ctx.restore();
                    });

                    // Pass 2: Draw all pill badge containers and text on top (prevents overlapping lines or occluded text)
                    visibleEvents.forEach((ev) => {
                        ctx.save();

                        // Pill background & border
                        ctx.fillStyle = isDark ? 'rgba(15, 23, 42, 0.95)' : 'rgba(255, 255, 255, 0.96)';
                        ctx.strokeStyle = ev.eventBorder;
                        ctx.lineWidth = 1.5;
                        drawRoundRect(ctx, ev.badgeX, ev.badgeY, ev.badgeW, ev.badgeH, 6);

                        // Pill text: Title
                        ctx.font = 'bold 10px Inter, -apple-system, sans-serif';
                        ctx.fillStyle = ev.eventBorder;
                        ctx.textAlign = 'center';
                        ctx.fillText(ev.line1, ev.badgeX + ev.badgeW / 2, ev.badgeY + 12);

                        // Pill text: Subtitle (topography altitude angle & cardinal)
                        ctx.font = '9px Inter, -apple-system, sans-serif';
                        ctx.fillStyle = textMuted;
                        ctx.fillText(ev.line2, ev.badgeX + ev.badgeW / 2, ev.badgeY + 23);

                        ctx.restore();
                    });
                }

                // 6. Interactive Hover Cursor & Info Tooltip
                if (mouseX >= 0 && mouseX <= width && mouseY >= 0 && mouseY <= height) {
                    // Check if mouse is hovering near any visible peak marker dot (within 18px) or over its label badge
                    let hoveredPeak = null;
                    let minPeakDist = 18;

                    for (let i = 0; i < visiblePeakBadges.length; i++) {
                        const b = visiblePeakBadges[i];
                        const d = Math.hypot(mouseX - b.px, mouseY - b.py);
                        const inBadge = (mouseX >= b.badgeX && mouseX <= b.badgeX + b.badgeW &&
                                         mouseY >= b.badgeY && mouseY <= b.badgeY + b.badgeH);
                        if (inBadge) {
                            hoveredPeak = { peak: b.peak, px: b.px, py: b.py, badge: b };
                            minPeakDist = 0;
                            break;
                        } else if (d < minPeakDist) {
                            minPeakDist = d;
                            hoveredPeak = { peak: b.peak, px: b.px, py: b.py, badge: b };
                        }
                    }

                    if (!hoveredPeak) {
                        for (let i = 0; i < peaks360.length; i++) {
                            const p = peaks360[i];
                            const pAz = p.azimuth ?? p.Azimuth ?? 0;
                            const px = azToX(pAz);
                            if (px === null || px < -20 || px > width + 20) continue;
                            const pAlt = p.altitudeAngle ?? p.AltitudeAngle ?? 0;
                            const py = altToY(pAlt);
                            const d = Math.hypot(mouseX - px, mouseY - py);
                            if (d < minPeakDist) {
                                minPeakDist = d;
                                hoveredPeak = { peak: p, px, py };
                            }
                        }
                    }

                    if (hoveredPeak) {
                        const hp = hoveredPeak.peak;
                        const hpx = hoveredPeak.px;
                        const hpy = hoveredPeak.py;
                        const hpIsSky = hp.isSkyline ?? hp.IsSkyline ?? true;
                        const hpName = hp.name ?? hp.Name ?? 'Summit';
                        const hpElevM = hp.elevationM ?? hp.ElevationM ?? 0;
                        const hpElevFt = hp.elevationFt ?? hp.ElevationFt ?? (hpElevM * 3.28084);
                        const hpDistKm = hp.distanceKm ?? hp.DistanceKm ?? 0;
                        const hpDistMi = hp.distanceMi ?? hp.DistanceMi ?? (hpDistKm * 0.621371);
                        const hpAz = hp.azimuth ?? hp.Azimuth ?? 0;
                        const hpAlt = hp.altitudeAngle ?? hp.AltitudeAngle ?? 0;

                        const accentColor = hpIsSky
                            ? (isDark ? '#38BDF8' : '#0284C7')
                            : (isDark ? '#34D399' : '#059669');

                        // Draw glowing target ring around summit marker
                        ctx.save();
                        ctx.strokeStyle = accentColor;
                        ctx.lineWidth = 2;
                        ctx.beginPath();
                        ctx.arc(hpx, hpy, 7, 0, Math.PI * 2);
                        ctx.stroke();

                        // Highlight badge border if hovering over or near badge
                        if (hoveredPeak.badge) {
                            const hb = hoveredPeak.badge;
                            ctx.strokeStyle = accentColor;
                            ctx.lineWidth = 1.8;
                            drawRoundRect(ctx, hb.badgeX, hb.badgeY, hb.badgeW, hb.badgeH, 6);
                        }
                        ctx.restore();

                        tooltip.style.display = 'block';
                        tooltip.style.backgroundColor = isDark ? 'rgba(15, 23, 42, 0.96)' : 'rgba(255, 255, 255, 0.98)';
                        tooltip.style.color = isDark ? '#F8FAFC' : '#18181B';
                        tooltip.style.border = `1px solid ${accentColor}`;
                        tooltip.style.left = `${Math.min(window.innerWidth - 240, rect.left + hpx + 12)}px`;
                        tooltip.style.top = `${Math.max(10, rect.top + Math.min(hpy, height - 90))}px`;

                        const elevText = isMetric
                            ? `${Math.round(hpElevM).toLocaleString()} m`
                            : `${Math.round(hpElevFt).toLocaleString()} ft`;
                        const distText = isMetric
                            ? `${hpDistKm.toFixed(1)} km`
                            : `${hpDistMi.toFixed(1)} mi`;
                        const kindText = hpIsSky ? 'Skyline Summit' : 'Visible Summit';

                        tooltip.innerHTML = `
                            <div style="font-weight:700; color:${accentColor}; margin-bottom:2px; font-size:12px;">
                                ${hpName}
                            </div>
                            <div style="font-size:10px; opacity:0.8; margin-bottom:4px; text-transform:uppercase; letter-spacing:0.5px;">
                                ${kindText} · ${Math.round(hpAz)}° ${getCardinal(hpAz)}
                            </div>
                            <div style="font-size:11px; opacity:0.9;">
                                Elev: <b>${elevText}</b> &nbsp;|&nbsp; Dist: <b>${distText}</b>
                            </div>
                            <div style="font-size:11px; opacity:0.75; margin-top:2px;">
                                Altitude Angle: <b>${hpAlt >= 0 ? '+' : ''}${hpAlt.toFixed(1)}°</b>
                            </div>
                        `;
                    } else {
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
                    }
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

                    // If the cursor is over any menu or control in front of the canvas, suppress tooltip and hover indicator
                    const el = document.elementFromPoint(e.clientX, e.clientY);
                    if (el && el !== canvas) {
                        if (mouseX !== -1) {
                            mouseX = -1;
                            mouseY = -1;
                            if (tooltip) tooltip.style.display = 'none';
                            render();
                        }
                        return;
                    }

                    mouseX = e.clientX - rect.left;
                    mouseY = e.clientY - rect.top;
                    render();
                } else {
                    if (mouseX !== -1) {
                        mouseX = -1;
                        mouseY = -1;
                        if (tooltip) tooltip.style.display = 'none';
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
                if (tooltip) tooltip.style.display = 'none';
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
