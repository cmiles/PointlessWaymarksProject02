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
/// A motion property that holds its value by reference: the value is not interpolated, so assigning
/// a new value snaps to it. Used for properties where animation lives inside the value itself (for
/// example a <see cref="Painting.Paint"/> reference, whose own properties are motion properties)
/// rather than by blending one value instance into another. How a change of the reference behaves
/// (snap vs. animate) is left to the owner — e.g. via the generated <c>On…Changed</c> hook.
/// </summary>
/// <typeparam name="T">The property type.</typeparam>
/// <remarks>
/// Initializes a new instance of the <see cref="ByReferenceMotionProperty{T}"/> class.
/// </remarks>
/// <param name="defaultValue">The default value.</param>
public class ByReferenceMotionProperty<T>(T defaultValue = default!)
    : MotionProperty<T>(defaultValue)
{
    /// <inheritdoc cref="MotionProperty{T}.CanTransitionate"/>
    protected override bool CanTransitionate => false;

    /// <inheritdoc cref="MotionProperty{T}.OnGetMovement(float)" />
    // Never invoked: CanTransitionate is false, so GetMovement returns the value directly.
    protected override T OnGetMovement(float progress) => ToValue;
}
