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

namespace LiveChartsCore.Motion;

/// <summary>
/// Defines the <see cref="float"/> array motion property class. Each element is interpolated
/// independently.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="FloatArrayMotionProperty"/> class.
/// </remarks>
/// <param name="defaultValue">The default value.</param>
public class FloatArrayMotionProperty(float[]? defaultValue = null)
    : MotionProperty<float[]?>(defaultValue)
{
    /// <inheritdoc cref="MotionProperty{T}.CanTransitionate"/>
    // Only equal-length, non-null arrays interpolate; otherwise the transition is skipped and
    // the target value is used directly (a change to a different-length array, or to/from null,
    // snaps).
    protected override bool CanTransitionate =>
        FromValue is not null && ToValue is not null && FromValue.Length == ToValue.Length;

    /// <inheritdoc cref="MotionProperty{T}.OnGetMovement(float)" />
    protected override float[] OnGetMovement(float progress)
    {
        var from = FromValue!;
        var to = ToValue!;

        var result = new float[to.Length];
        for (var i = 0; i < to.Length; i++)
            result[i] = from[i] + progress * (to[i] - from[i]);

        return result;
    }
}
