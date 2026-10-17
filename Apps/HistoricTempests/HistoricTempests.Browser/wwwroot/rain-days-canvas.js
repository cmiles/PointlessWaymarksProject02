// Hardware-Accelerated 2D Canvas Renderer for Multi-Year Rain Days Heatmap
(function () {
    window.initRainDaysCanvas = function (container, jsonString) {
        if (!container) return;

        let data;
        try {
            data = typeof jsonString === 'string' ? JSON.parse(jsonString) : jsonString;
        } catch (e) {
            console.error('Invalid rain days JSON', e);
            return;
        }

        container.innerHTML = '';
        container.style.position = 'relative';
        container.style.width = '100%';
        container.style.overflow = 'visible';

        const canvas = document.createElement('canvas');
        canvas.style.display = 'block';
        canvas.style.cursor = 'default';
        container.appendChild(canvas);

        // Floating Tooltip singleton attached to body
        let tooltip = document.getElementById('rainDaysGlobalTooltip');
        if (!tooltip) {
            tooltip = document.createElement('div');
            tooltip.id = 'rainDaysGlobalTooltip';
            tooltip.style.position = 'fixed';
            tooltip.style.display = 'none';
            tooltip.style.pointerEvents = 'none';
            tooltip.style.zIndex = '999999';
            tooltip.style.background = 'rgba(15, 23, 42, 0.95)';
            tooltip.style.color = '#FFFFFF';
            tooltip.style.padding = '8px 12px';
            tooltip.style.borderRadius = '6px';
            tooltip.style.fontSize = '12px';
            tooltip.style.fontWeight = '500';
            tooltip.style.fontFamily = 'Inter, -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif';
            tooltip.style.boxShadow = '0 10px 15px -3px rgba(0, 0, 0, 0.3), 0 4px 6px -4px rgba(0, 0, 0, 0.2)';
            tooltip.style.border = '1px solid rgba(255, 255, 255, 0.12)';
            tooltip.style.transition = 'opacity 0.08s ease';
            document.body.appendChild(tooltip);
        }

        let hitZones = [];
        let isDark = false;
        let isMetric = false;

        function render() {
            const rect = container.getBoundingClientRect();
            const containerWidth = rect.width > 50 ? rect.width : (window.innerWidth ? Math.min(1168, window.innerWidth - 48) : 900);

            const years = data.years || [2026, 2025, 2024, 2023];
            const slots = data.slots || [];
            isMetric = !!data.isMetric;
            isDark = !!data.isDark;
            const maxRain = data.maxRain || (isMetric ? 10.0 : 0.5);
            const maxLight = data.maxLight || 50.0;

            const usableWidth = Math.max(200.0, containerWidth - 4.0);

            // Month name before each day is eliminated; only day number (1..31) is shown!
            let dateColWidth = 30.0;
            if (usableWidth > 800) dateColWidth = 32.0;
            else if (usableWidth > 500) dateColWidth = 26.0;
            else dateColWidth = 22.0;

            const subColGap = usableWidth > 700 ? 2.0 : 1.5;
            const yearColGap = usableWidth > 700 ? 12.0 : (usableWidth > 450 ? 8.0 : 5.0);
            const totalGaps = (years.length * subColGap) + ((years.length - 1) * yearColGap);

            const availableForCols = usableWidth - dateColWidth - totalGaps;
            const colWidth = Math.max(8.0, availableForCols / (years.length * 2));
            const yearWidth = (colWidth * 2) + subColGap;
            const totalContentWidth = dateColWidth + (years.length * yearWidth) + ((years.length - 1) * yearColGap);

            // Compact header sizing
            const isCompact = usableWidth < 650;
            const isVerySmall = usableWidth < 450 || colWidth < 28;

            const headerHeight = isCompact ? 36.0 : 42.0;
            const yearBoxH = isCompact ? 15.0 : 18.0;
            const subHeaderBoxH = isCompact ? 15.0 : 18.0;
            const subHeaderY = isCompact ? 18.0 : 21.0;

            const rowHeight = 16.0;
            const rowGap = 2.0;
            const monthBannerHeight = isCompact ? 20.0 : 22.0;

            // Calculate total canvas height
            let simY = headerHeight + 4.0;
            let lastMonth = '';
            for (let i = 0; i < slots.length; i++) {
                const m = slots[i].m;
                if (m !== lastMonth) {
                    lastMonth = m;
                    simY += monthBannerHeight + 4.0;
                }
                simY += rowHeight + rowGap;
            }
            const totalCanvasHeight = simY + 20.0;

            // Handle High-DPI / Retina Crispness
            const dpr = window.devicePixelRatio || 1;
            canvas.width = Math.round(totalContentWidth * dpr);
            canvas.height = Math.round(totalCanvasHeight * dpr);
            canvas.style.width = totalContentWidth + 'px';
            canvas.style.height = totalCanvasHeight + 'px';
            canvas.style.background = isDark ? '#1E293B' : '#FFFFFF';
            container.style.height = totalCanvasHeight + 'px';
            container.style.minHeight = totalCanvasHeight + 'px';
            container.style.background = isDark ? '#1E293B' : '#FFFFFF';

            const ctx = canvas.getContext('2d');
            ctx.setTransform(1, 0, 0, 1, 0, 0);
            ctx.scale(dpr, dpr);
            ctx.clearRect(0, 0, totalContentWidth, totalCanvasHeight);

            hitZones = [];

            // --- 1. RENDER COMPACT HEADER ---
            // Subheaders: auto shrink, eliminate icons if needed
            let rainLabel, ltLabel, subHeaderFontSize;
            if (colWidth >= 52) {
                rainLabel = '🌧 Rain';
                ltLabel = '⚡ Strikes';
                subHeaderFontSize = 9.0;
            } else if (colWidth >= 28) {
                // Just icons
                rainLabel = '🌧';
                ltLabel = '⚡';
                subHeaderFontSize = 9.5;
            } else if (colWidth >= 16) {
                // Icons eliminated! Clean minimal letters
                rainLabel = 'R';
                ltLabel = 'S';
                subHeaderFontSize = colWidth >= 22 ? 8.0 : 7.0;
            } else {
                // Ultra-small: omit text, color badge indicates metric
                rainLabel = '';
                ltLabel = '';
                subHeaderFontSize = 0;
            }

            for (let yi = 0; yi < years.length; yi++) {
                const yr = years[yi];
                const yrX = dateColWidth + yi * (yearWidth + yearColGap);

                // Year auto-shrink
                let yrLabel = String(yr);
                if (yearWidth < 36) {
                    yrLabel = String(yr % 100);
                } else if (yearWidth < 54) {
                    yrLabel = `'${yr % 100}`;
                }
                const yrFontSize = yearWidth >= 64 ? 10.5 : (yearWidth >= 42 ? 9.0 : (yearWidth >= 30 ? 7.5 : 6.5));

                // Year Label at Top
                ctx.fillStyle = isDark ? '#0F172A' : '#F1F5F9';
                ctx.strokeStyle = isDark ? '#334155' : '#CBD5E1';
                ctx.lineWidth = 1;
                drawRoundedRect(ctx, yrX, 2, yearWidth, yearBoxH, 3, true, true);

                ctx.fillStyle = isDark ? '#F8FAFC' : '#1E293B';
                ctx.font = `bold ${yrFontSize}px Inter, sans-serif`;
                ctx.textAlign = 'center';
                ctx.textBaseline = 'middle';
                ctx.fillText(yrLabel, yrX + yearWidth / 2, 2 + yearBoxH / 2);

                // Rain sub-header
                ctx.fillStyle = isDark ? '#1E3A8A' : '#EFF6FF';
                ctx.strokeStyle = isDark ? '#3B82F6' : '#BFDBFE';
                drawRoundedRect(ctx, yrX, subHeaderY, colWidth, subHeaderBoxH, 2.5, true, true);
                if (rainLabel) {
                    ctx.fillStyle = isDark ? '#93C5FD' : '#1D4ED8';
                    ctx.font = `600 ${subHeaderFontSize}px Inter, sans-serif`;
                    ctx.fillText(rainLabel, yrX + colWidth / 2, subHeaderY + subHeaderBoxH / 2);
                }

                // Lightning sub-header
                const ltX = yrX + colWidth + subColGap;
                ctx.fillStyle = isDark ? '#78350F' : '#FEF3C7';
                ctx.strokeStyle = isDark ? '#B45309' : '#FDE68A';
                drawRoundedRect(ctx, ltX, subHeaderY, colWidth, subHeaderBoxH, 2.5, true, true);
                if (ltLabel) {
                    ctx.fillStyle = isDark ? '#FDE68A' : '#B45309';
                    ctx.font = `600 ${subHeaderFontSize}px Inter, sans-serif`;
                    ctx.fillText(ltLabel, ltX + colWidth / 2, subHeaderY + subHeaderBoxH / 2);
                }

                // Vertical divider line between years in header
                if (yi < years.length - 1) {
                    const dividerX = Math.round(yrX + yearWidth + (yearColGap / 2.0));
                    ctx.strokeStyle = isDark ? '#334155' : '#CBD5E1';
                    ctx.lineWidth = 1;
                    ctx.beginPath();
                    ctx.moveTo(dividerX + 0.5, 1);
                    ctx.lineTo(dividerX + 0.5, headerHeight - 1);
                    ctx.stroke();
                }
            }

            // Header bottom divider line
            ctx.strokeStyle = isDark ? '#334155' : '#CBD5E1';
            ctx.lineWidth = 1;
            ctx.beginPath();
            ctx.moveTo(0, headerHeight - 0.5);
            ctx.lineTo(totalContentWidth, headerHeight - 0.5);
            ctx.stroke();

            // --- 2. RENDER DAYS & MONTHS ---
            let currentY = headerHeight + 4.0;
            lastMonth = '';

            for (let si = 0; si < slots.length; si++) {
                const s = slots[si];
                const monthPart = s.m;

                if (monthPart !== lastMonth) {
                    lastMonth = monthPart;

                    // Month Banner
                    ctx.fillStyle = isDark ? '#0F172A' : '#F1F5F9';
                    ctx.fillRect(0, currentY, totalContentWidth, monthBannerHeight);

                    ctx.strokeStyle = isDark ? '#334155' : '#E2E8F0';
                    ctx.lineWidth = 1;
                    ctx.beginPath();
                    ctx.moveTo(0, currentY + 0.5);
                    ctx.lineTo(totalContentWidth, currentY + 0.5);
                    ctx.moveTo(0, currentY + monthBannerHeight - 0.5);
                    ctx.lineTo(totalContentWidth, currentY + monthBannerHeight - 0.5);
                    ctx.stroke();

                    const monthNames = { 'Jan': 'JANUARY', 'Feb': 'FEBRUARY', 'Mar': 'MARCH', 'Apr': 'APRIL', 'May': 'MAY', 'Jun': 'JUNE', 'Jul': 'JULY', 'Aug': 'AUGUST', 'Sep': 'SEPTEMBER', 'Oct': 'OCTOBER', 'Nov': 'NOVEMBER', 'Dec': 'DECEMBER' };
                    const bannerText = usableWidth >= 550 ? (monthNames[monthPart] || monthPart.toUpperCase()) : monthPart.toUpperCase();
                    ctx.fillStyle = isDark ? '#CBD5E1' : '#475569';
                    ctx.font = `bold ${isCompact ? 9 : 10}px Inter, sans-serif`;
                    ctx.textAlign = 'left';
                    ctx.textBaseline = 'middle';
                    ctx.fillText(bannerText, 6, currentY + monthBannerHeight / 2);

                    currentY += monthBannerHeight + 4.0;
                }

                // Date label: just day number (1..31) - month is handled by horizontal month bar!
                const dayNum = s.d.indexOf(' ') > 0 ? s.d.split(' ')[1] : s.d;
                const dateFontSize = dateColWidth >= 28 ? 9.5 : (dateColWidth >= 24 ? 8.5 : 7.5);
                ctx.fillStyle = s.ref ? '#38BDF8' : (isDark ? '#94A3B8' : '#64748B');
                ctx.font = `${s.ref ? 'bold' : 'normal'} ${dateFontSize}px Inter, sans-serif`;
                ctx.textAlign = 'right';
                ctx.textBaseline = 'middle';
                ctx.fillText(dayNum, dateColWidth - 3, currentY + rowHeight / 2);

                // Year cells
                for (let yi = 0; yi < years.length; yi++) {
                    const yr = years[yi];
                    const yrX = dateColWidth + yi * (yearWidth + yearColGap);
                    const yrData = s.y ? s.y[yr] : null;

                    const rainVal = yrData ? yrData.r : null;
                    const ltVal = yrData ? yrData.l : null;

                    // Rain cell
                    renderCell(ctx, yrX, currentY, colWidth, rowHeight, yr, 'Rain', rainVal, maxRain, isMetric, isDark, s, dayNum, isVerySmall);
                    // Lightning cell
                    renderCell(ctx, yrX + colWidth + subColGap, currentY, colWidth, rowHeight, yr, 'Lightning', ltVal, maxLight, isMetric, isDark, s, dayNum, isVerySmall);
                }

                currentY += rowHeight + rowGap;
            }

            // --- 3. VERTICAL DIVIDER LINES BETWEEN YEARS ---
            for (let yi = 0; yi < years.length - 1; yi++) {
                const yrX = dateColWidth + yi * (yearWidth + yearColGap);
                const dividerX = Math.round(yrX + yearWidth + (yearColGap / 2.0));
                ctx.strokeStyle = isDark ? '#334155' : '#E2E8F0';
                ctx.lineWidth = 1;
                ctx.beginPath();
                ctx.moveTo(dividerX + 0.5, headerHeight);
                ctx.lineTo(dividerX + 0.5, currentY);
                ctx.stroke();
            }
        }

        function renderCell(ctx, x, y, w, h, yr, metric, val, maxVal, isMetric, isDark, s, dayNum, isVerySmall) {
            if (val === null || val === undefined) {
                ctx.fillStyle = isDark ? '#0F172A' : '#F8FAFC';
                ctx.strokeStyle = isDark ? '#1E293B' : '#F1F5F9';
                ctx.lineWidth = 0.8;
                drawRoundedRect(ctx, x, y, w, h, 2.5, true, true);
                return;
            }

            const isZero = val <= 0.0001;

            if (isZero) {
                ctx.fillStyle = isDark ? '#0F172A' : '#F8FAFC';
                ctx.strokeStyle = isDark ? '#334155' : '#E2E8F0';
                ctx.lineWidth = 1;
                drawRoundedRect(ctx, x, y, w, h, 2.5, true, true);
            } else {
                const style = getCellBrush(metric, val, maxVal);
                ctx.fillStyle = style.fill;
                ctx.strokeStyle = style.border;
                ctx.lineWidth = 1.2;
                drawRoundedRect(ctx, x, y, w, h, 2.5, true, true);

                // Drop numbers in heatmap boxes at very small sizes
                const dropNumbers = w < 30 || isVerySmall;

                if (!dropNumbers && w >= 30) {
                    let valText = '';
                    if (metric === 'Rain') {
                        valText = isMetric
                            ? val.toFixed(1)
                            : (val < 0.1 ? val.toFixed(2).replace(/^0/, '') : val.toFixed(2));
                    } else {
                        valText = val >= 1000 ? (val / 1000).toFixed(1) + 'k' : Math.round(val).toString();
                    }

                    const fontSize = w >= 44 ? 8.5 : (w >= 36 ? 7.5 : 6.5);
                    ctx.fillStyle = style.isDark ? '#FFFFFF' : (metric === 'Rain' ? '#0F172A' : '#451A03');
                    ctx.font = `bold ${fontSize}px Inter, sans-serif`;
                    ctx.textAlign = 'center';
                    ctx.textBaseline = 'middle';
                    ctx.fillText(valText, x + w / 2, y + h / 2);
                }
            }

            const unit = metric === 'Rain' ? (isMetric ? 'mm' : 'in') : 'strikes';
            const fullDateLabel = `${s.m} ${dayNum}`;
            const formattedTooltip = isZero
                ? `${fullDateLabel} (${yr}) ${metric}: 0 ${unit}`
                : (metric === 'Rain' ? `${fullDateLabel} (${yr}) Rain: ${val.toFixed(2)} ${unit}` : `${fullDateLabel} (${yr}) Lightning: ${Math.round(val).toLocaleString()} ${unit}`);

            hitZones.push({
                x: x,
                y: y,
                w: w,
                h: h,
                tip: formattedTooltip
            });
        }

        function getCellBrush(metric, val, maxVal) {
            if (metric === 'Rain') {
                const ratio = Math.sqrt(Math.max(0, Math.min(1.0, val / maxVal)));
                const r = Math.round(186 + (30 - 186) * ratio);
                const g = Math.round(230 + (58 - 230) * ratio);
                const b = Math.round(253 + (138 - 253) * ratio);
                const br = Math.max(0, r - 35);
                const bg = Math.max(0, g - 45);
                const bb = Math.max(0, b - 20);
                return {
                    fill: `rgb(${r},${g},${b})`,
                    border: `rgb(${br},${bg},${bb})`,
                    isDark: ratio >= 0.55
                };
            } else {
                const ratio = Math.pow(Math.max(0, Math.min(1.0, val / maxVal)), 0.42);
                const r = Math.round(254 + (180 - 254) * ratio);
                const g = Math.round(240 + (83 - 240) * ratio);
                const b = Math.round(138 + (9 - 138) * ratio);
                const br = Math.max(0, r - 45);
                const bg = Math.max(0, g - 65);
                const bb = Math.max(0, b - 20);
                return {
                    fill: `rgb(${r},${g},${b})`,
                    border: `rgb(${br},${bg},${bb})`,
                    isDark: ratio >= 0.65
                };
            }
        }

        function drawRoundedRect(ctx, x, y, width, height, radius, fill, stroke) {
            ctx.beginPath();
            ctx.moveTo(x + radius, y);
            ctx.lineTo(x + width - radius, y);
            ctx.quadraticCurveTo(x + width, y, x + width, y + radius);
            ctx.lineTo(x + width, y + height - radius);
            ctx.quadraticCurveTo(x + width, y + height, x + width - radius, y + height);
            ctx.lineTo(x + radius, y + height);
            ctx.quadraticCurveTo(x, y + height, x, y + height - radius);
            ctx.lineTo(x, y + radius);
            ctx.quadraticCurveTo(x, y, x + radius, y);
            ctx.closePath();
            if (fill) ctx.fill();
            if (stroke) ctx.stroke();
        }

        function handlePointer(clientX, clientY) {
            const rect = canvas.getBoundingClientRect();
            const mx = clientX - rect.left;
            const my = clientY - rect.top;

            let found = null;
            for (let i = 0; i < hitZones.length; i++) {
                const z = hitZones[i];
                if (my >= z.y && my <= z.y + z.h && mx >= z.x && mx <= z.x + z.w) {
                    found = z;
                    break;
                }
            }

            if (found) {
                tooltip.textContent = found.tip;
                tooltip.style.display = 'block';
                tooltip.style.left = Math.min(window.innerWidth - 220, clientX + 14) + 'px';
                tooltip.style.top = (clientY + 14) + 'px';
            } else {
                tooltip.style.display = 'none';
            }
        }

        canvas.onmousemove = function (e) {
            handlePointer(e.clientX, e.clientY);
        };

        canvas.onmouseleave = function () {
            tooltip.style.display = 'none';
        };

        canvas.ontouchstart = function (e) {
            if (e.touches && e.touches.length > 0) {
                handlePointer(e.touches[0].clientX, e.touches[0].clientY);
            }
        };

        canvas.ontouchmove = function (e) {
            if (e.touches && e.touches.length > 0) {
                handlePointer(e.touches[0].clientX, e.touches[0].clientY);
            }
        };

        canvas.ontouchend = function () {
            setTimeout(function () {
                tooltip.style.display = 'none';
            }, 2500);
        };

        // Attach ResizeObserver to container for instant re-render on resize
        if (window.ResizeObserver && !container._rainDaysObserver) {
            container._rainDaysObserver = new ResizeObserver(() => {
                render();
            });
            container._rainDaysObserver.observe(container);
        }

        render();
    };
})();
