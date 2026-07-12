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

namespace LiveChartsCore.Generators;

/// <summary>
/// Registers a property as a visual-state target without making it a motion property. LiveCharts
/// generates a <see cref="LiveChartsCore.Motion.PropertyDefinition"/> for it (with a null motion
/// getter) and adds it to the type's property-definition collection, so a visual state can set and
/// restore it by name. The property itself is left untouched — declare its accessors yourself.
/// Use this for properties whose value is not interpolated (for example a paint reference); use
/// <see cref="MotionPropertyAttribute"/> when the value should also animate.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class StatePropertyAttribute : Attribute
{
}
