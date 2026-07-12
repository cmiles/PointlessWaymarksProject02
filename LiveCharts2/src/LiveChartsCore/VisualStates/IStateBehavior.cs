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

using LiveChartsCore.Drawing;

namespace LiveChartsCore.VisualStates;

/// <summary>
/// Defines an imperative side of a visual state, for work that a value setter can not express.
/// </summary>
/// <remarks>
/// A value setter assigns a registered motion property by name (and is restored automatically when
/// the state is cleared); some effects can not be modeled that way — for example animating a fill
/// color requires reaching into the paint's own motion property instead of replacing the paint
/// reference. A behavior runs arbitrary work when the state is applied and pairs it with the work to
/// undo it when the state is removed.
/// <para>
/// A single behavior instance is shared by every target the owning state is applied to (one state
/// definition per series is applied to each point), so a behavior must be stateless. If it needs to
/// remember a per-target value in order to restore it, key that value on the target (for example via
/// a <see cref="System.Runtime.CompilerServices.ConditionalWeakTable{TKey, TValue}"/>) rather than in
/// an instance field.
/// </para>
/// <para>
/// A behavior should mutate the per-target visual it is handed, not a shared/theme paint instance;
/// mutating a shared paint would affect every point that draws with it.
/// </para>
/// </remarks>
public interface IStateBehavior
{
    /// <summary>
    /// Called once when the state becomes active on the given target.
    /// </summary>
    /// <param name="target">The target the state was applied to.</param>
    void OnStateApplied(Animatable target);

    /// <summary>
    /// Called once when the state is removed from the given target; should undo <see cref="OnStateApplied"/>.
    /// </summary>
    /// <param name="target">The target the state was removed from.</param>
    void OnStateRemoved(Animatable target);
}
