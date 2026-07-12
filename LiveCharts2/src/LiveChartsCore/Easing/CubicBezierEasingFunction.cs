// The MIT License(MIT)
//
// Copyright(c) 2021 Alberto Rodriguez Orozco & LiveCharts Contributors
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System;

namespace LiveChartsCore.Easing;

/// <summary>
/// Defines the CubicBezierEasingFunction.
/// </summary>
public static class CubicBezierEasingFunction
{
    // Number of intervals in the precomputed lookup table. The eased value y is solved once
    // per evenly-spaced x at build time; the returned delegate then only does an index + a
    // linear interpolation, which is what makes it cheap to call every frame.
    private const int LutIntervals = 256;

    // Build-time root solve for x(t) == x. Cheap to do once per bucket; never runs per frame, so
    // we can afford a robust bracketed solve rather than a few raw Newton steps.
    private const int BuildSolveIterations = 24;
    private const float MinSlope = 1e-6f;
    private const float SolveTolerance = 1e-6f;

    /// <summary>
    /// Builds a bezier easing function.
    /// </summary>
    /// <param name="mX1">The m x1.</param>
    /// <param name="mY1">The m y1.</param>
    /// <param name="mX2">The m x2.</param>
    /// <param name="mY2">The m y2.</param>
    /// <returns></returns>
    /// <exception cref="Exception">Bezier x values must be in [0, 1] range</exception>
    public static Func<float, float> BuildBezierEasingFunction(float mX1, float mY1, float mX2, float mY2)
    {
        if (mX1 < 0 || mX1 > 1 || mX2 < 0 || mX2 > 1)
            throw new Exception("Bezier x values must be in [0, 1] range");

        if (mX1 == mY1 && mX2 == mY2)
            return LinearEasing;

        // Polynomial coefficients of the cubic bezier (the first and last control points are
        // fixed at 0 and 1), in Horner form: value(t) = ((a * t + b) * t + c) * t. They are
        // constant for this curve, so we compute them once here instead of on every sample as
        // the original port did.
        var ax = 1f - 3f * mX2 + 3f * mX1;
        var bx = 3f * mX2 - 6f * mX1;
        var cx = 3f * mX1;
        var ay = 1f - 3f * mY2 + 3f * mY1;
        var by = 3f * mY2 - 6f * mY1;
        var cy = 3f * mY1;

        static float Calc(float t, float a, float b, float c) => ((a * t + b) * t + c) * t;
        static float Slope(float t, float a, float b, float c) => (3f * a * t + 2f * b) * t + c;

        // Solve x(t) == x. x(t) is monotonic non-decreasing on [0, 1] for an easing curve, so we
        // keep a [lo, hi] bracket and take a Newton step only when it is safe (non-tiny slope and
        // the step stays inside the bracket); otherwise we bisect. This stays correct even for
        // degenerate curves like mX1 == mX2 == 0 (x(t) == t^3), where a raw Newton seed overshoots.
        static float SolveT(float x, float a, float b, float c)
        {
            float lo = 0f, hi = 1f, t = x;
            for (var k = 0; k < BuildSolveIterations; k++)
            {
                var fx = Calc(t, a, b, c) - x;
                if (Math.Abs(fx) < SolveTolerance) break;
                if (fx > 0f) hi = t; else lo = t;

                var slope = Slope(t, a, b, c);
                var next = Math.Abs(slope) < MinSlope ? float.NaN : t - fx / slope;
                t = next > lo && next < hi ? next : 0.5f * (lo + hi);
            }
            return t;
        }

        // Precompute the eased y for evenly-spaced x: solve x(t) == x, then store y(t). The
        // endpoints are exact: Calc(0) == 0 and Calc(1) == 1 on both axes.
        var lut = new float[LutIntervals + 1];
        for (var i = 0; i <= LutIntervals; i++)
        {
            var x = i / (float)LutIntervals;
            lut[i] = Calc(SolveT(x, ax, bx, cx), ay, by, cy);
        }

        return x =>
        {
            if (x <= 0f) return 0f;
            if (x >= 1f) return 1f;

            var f = x * LutIntervals;
            var i = (int)f;
            // linear interpolation between the two samples surrounding x.
            return lut[i] + (lut[i + 1] - lut[i]) * (f - i);
        };
    }

    private static float LinearEasing(float x) => x;
}
