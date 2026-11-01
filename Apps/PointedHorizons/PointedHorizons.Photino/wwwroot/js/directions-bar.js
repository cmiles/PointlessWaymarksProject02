// Hardware-Accelerated Continuous 360° Directions Compass Bar for PointedHorizons
(function () {
    let currentInstance = null;

    window.setDirectionsBarParams = function (newAz, newFov, newDark) {
        if (currentInstance && currentInstance.setParams) {
            currentInstance.setParams(newAz, newFov, newDark);
        }
    };

    window.initDirectionsBar = function (container, initialAz, initialFov, isDarkTheme, onPanCallback) {
        if (!container) return;

        container.innerHTML = '';
        container.style.position = 'relative';
        container.style.width = '100%';
        container.style.height = '100%';
        container.style.overflow = 'hidden';
        container.style.userSelect = 'none';

        const canvas = document.createElement('canvas');
        canvas.style.display = 'block';
        canvas.style.width = '100%';
        canvas.style.height = '100%';
        canvas.style.cursor = 'grab';
        container.appendChild(canvas);

        const ctx = canvas.getContext('2d', { alpha: false });

        let centerAz = initialAz ?? 270.0;
        let fov = initialFov ?? 90.0;
        let isDark = isDarkTheme ?? false;

        let isDragging = false;
        let dragStartX = 0;
        let dragStartAz = centerAz;

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

        const compassPoints = [
            { deg: 0, label: 'N', isMajor: true, isNorth: true },
            { deg: 22.5, label: 'NNE', isMajor: false },
            { deg: 45, label: 'NE', isMajor: true },
            { deg: 67.5, label: 'ENE', isMajor: false },
            { deg: 90, label: 'E', isMajor: true },
            { deg: 112.5, label: 'ESE', isMajor: false },
            { deg: 135, label: 'SE', isMajor: true },
            { deg: 157.5, label: 'SSE', isMajor: false },
            { deg: 180, label: 'S', isMajor: true },
            { deg: 202.5, label: 'SSW', isMajor: false },
            { deg: 225, label: 'SW', isMajor: true },
            { deg: 247.5, label: 'WSW', isMajor: false },
            { deg: 270, label: 'W', isMajor: true },
            { deg: 292.5, label: 'WNW', isMajor: false },
            { deg: 315, label: 'NW', isMajor: true },
            { deg: 337.5, label: 'NNW', isMajor: false }
        ];

        function render() {
            try {
                const rect = container.getBoundingClientRect();
                const width = Math.max(100, rect.width || 100);
                const height = Math.max(24, rect.height || 36);

                const dpr = window.devicePixelRatio || 1;
                canvas.width = Math.round(width * dpr);
                canvas.height = Math.round(height * dpr);
                ctx.resetTransform();
                ctx.scale(dpr, dpr);

                const startAz = centerAz - fov / 2.0;
                const baselineY = height - 6;

                // Background
                ctx.fillStyle = isDark ? '#0A0E17' : '#F8FAFC';
                ctx.fillRect(0, 0, width, height);

                // Top Border line
                ctx.strokeStyle = isDark ? '#1E293B' : '#E2E8F0';
                ctx.lineWidth = 1;
                ctx.beginPath();
                ctx.moveTo(0, 0.5);
                ctx.lineTo(width, 0.5);
                ctx.stroke();

                // Baseline
                ctx.strokeStyle = isDark ? 'rgba(255, 255, 255, 0.18)' : 'rgba(0, 0, 0, 0.15)';
                ctx.lineWidth = 1;
                ctx.beginPath();
                ctx.moveTo(0, baselineY);
                ctx.lineTo(width, baselineY);
                ctx.stroke();

                // Helper: Degree to screen X with wraparound margin
                function degToScreenX(deg) {
                    let d = (deg - startAz) % 360;
                    if (d < 0) d += 360;
                    const marginDeg = (30 / width) * fov;
                    if (d > 360 - marginDeg) d -= 360;
                    if (d >= -marginDeg && d <= fov + marginDeg) {
                        return (d / fov) * width;
                    }
                    return null;
                }

                // Dynamic tick resolution based on current FOV
                let tickStep = 1;
                if (fov > 120) tickStep = 5;
                else if (fov > 75) tickStep = 2;
                else tickStep = 1;

                // Draw Degree Ticks
                ctx.save();
                const tickAltColor = isDark ? 'rgba(255, 255, 255, 0.16)' : 'rgba(0, 0, 0, 0.14)';
                const textDegreeColor = isDark ? 'rgba(148, 163, 184, 0.75)' : 'rgba(113, 113, 122, 0.8)';

                for (let deg = 0; deg < 360; deg += tickStep) {
                    const isCardinal = (deg % 90 === 0);
                    const isIntercardinal = (deg % 45 === 0);

                    const x = degToScreenX(deg);
                    if (x === null) continue;

                    const is10Deg = (deg % 10 === 0);
                    const is5Deg = (deg % 5 === 0);

                    let tickH = 3;
                    if (is10Deg) tickH = 7;
                    else if (is5Deg) tickH = 4;

                    ctx.strokeStyle = is10Deg ? (isDark ? 'rgba(255, 255, 255, 0.45)' : 'rgba(24, 24, 27, 0.45)') : tickAltColor;
                    ctx.beginPath();
                    ctx.moveTo(x, baselineY);
                    ctx.lineTo(x, baselineY - tickH);
                    ctx.stroke();

                    // Degree numbers at 10° intervals when FOV <= 90°
                    if (is10Deg && !isCardinal && !isIntercardinal && fov <= 90) {
                        ctx.fillStyle = textDegreeColor;
                        ctx.font = '8px Inter, -apple-system, sans-serif';
                        ctx.textAlign = 'center';
                        ctx.fillText(`${deg}°`, x, baselineY - 9);
                    }
                }
                ctx.restore();

                // Draw Directional Markers (N, NE, E, SE, S, SW, W, NW, etc.)
                ctx.save();
                compassPoints.forEach(pt => {
                    if (!pt.isMajor && fov > 75) return;

                    const x = degToScreenX(pt.deg);
                    if (x === null) return;

                    const isNorth = pt.isNorth;
                    const isCardinal = (pt.deg % 90 === 0);
                    const tickLen = isCardinal ? 12 : (pt.isMajor ? 9 : 6);

                    // Marker Tick Mark
                    ctx.strokeStyle = isNorth
                        ? (isDark ? '#38BDF8' : '#DC2626')
                        : (isCardinal
                            ? (isDark ? '#F8FAFC' : '#18181B')
                            : (isDark ? '#94A3B8' : '#52525B'));
                    ctx.lineWidth = isCardinal ? 2 : 1.2;
                    ctx.beginPath();
                    ctx.moveTo(x, baselineY);
                    ctx.lineTo(x, baselineY - tickLen);
                    ctx.stroke();

                    // Label Text
                    ctx.textAlign = 'center';
                    if (isNorth) {
                        ctx.font = 'bold 11px Inter, -apple-system, sans-serif';
                        ctx.fillStyle = isDark ? '#38BDF8' : '#DC2626';
                        ctx.fillText(pt.label, x, baselineY - 14);
                    } else if (isCardinal) {
                        ctx.font = 'bold 11px Inter, -apple-system, sans-serif';
                        ctx.fillStyle = isDark ? '#F8FAFC' : '#18181B';
                        ctx.fillText(pt.label, x, baselineY - 13);
                    } else if (pt.isMajor) {
                        ctx.font = 'bold 9.5px Inter, -apple-system, sans-serif';
                        ctx.fillStyle = isDark ? '#CBD5E1' : '#334155';
                        ctx.fillText(pt.label, x, baselineY - 11);
                    } else {
                        ctx.font = '8.5px Inter, -apple-system, sans-serif';
                        ctx.fillStyle = isDark ? '#94A3B8' : '#64748B';
                        ctx.fillText(pt.label, x, baselineY - 9);
                    }
                });
                ctx.restore();

                // Center Indicator Triangle & Line
                const cx = width / 2;
                ctx.save();
                ctx.fillStyle = isDark ? '#38BDF8' : '#0284C7';
                ctx.beginPath();
                ctx.moveTo(cx, baselineY - 10);
                ctx.lineTo(cx - 4.5, baselineY);
                ctx.lineTo(cx + 4.5, baselineY);
                ctx.closePath();
                ctx.fill();

                ctx.strokeStyle = isDark ? '#38BDF8' : '#0284C7';
                ctx.lineWidth = 1.5;
                ctx.beginPath();
                ctx.moveTo(cx, baselineY - 10);
                ctx.lineTo(cx, baselineY - 14);
                ctx.stroke();

                // Center Heading Pill
                const headingText = `${Math.round(centerAz)}° ${getCardinal(centerAz)}`;
                ctx.font = 'bold 10px Inter, -apple-system, sans-serif';
                ctx.fillStyle = isDark ? '#38BDF8' : '#0284C7';
                ctx.textAlign = 'center';
                ctx.fillText(headingText, cx, 11);
                ctx.restore();

            } catch (e) {
                console.error('Directions bar render error:', e);
            }
        }

        // Mouse Drag to Pan Azimuth
        canvas.addEventListener('mousedown', function (e) {
            isDragging = true;
            dragStartX = e.clientX;
            dragStartAz = centerAz;
            canvas.style.cursor = 'grabbing';
        });

        window.addEventListener('mousemove', function (e) {
            if (isDragging) {
                const deltaX = e.clientX - dragStartX;
                const rect = canvas.getBoundingClientRect();
                const deltaAz = -(deltaX / Math.max(1, rect.width)) * fov;
                centerAz = normalizeAngle(dragStartAz + deltaAz);

                render();

                // Immediately update horizon canvas in the same frame
                if (window.setHorizonParams) {
                    window.setHorizonParams(centerAz);
                }

                if (onPanCallback) {
                    onPanCallback(centerAz);
                }
            }
        });

        window.addEventListener('mouseup', function () {
            if (isDragging) {
                isDragging = false;
                canvas.style.cursor = 'grab';
                if (onPanCallback) {
                    onPanCallback(centerAz);
                }
            }
        });

        // ResizeObserver
        if (window.ResizeObserver && !container._directionsObserver) {
            container._directionsObserver = new ResizeObserver(() => render());
            container._directionsObserver.observe(container);
        }

        currentInstance = {
            setParams: function (newAz, newFov, newDark) {
                if (newAz != null) centerAz = normalizeAngle(newAz);
                if (newFov != null) fov = Math.max(3, Math.min(160, newFov));
                if (newDark != null) isDark = newDark;
                render();
            }
        };

        render();
    };
})();

