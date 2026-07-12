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
using LiveChartsCore.Drawing;

namespace LiveChartsCore.VisualStates;

/// <summary>
/// An inline <see cref="IStateBehavior"/> built from a pair of delegates, for one-off behaviors that
/// do not warrant a dedicated type.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="StateBehavior"/> class. The same instance is shared by
/// every target the owning state is applied to, so the delegates must be stateless; see
/// <see cref="IStateBehavior"/> for how to keep per-target state.
/// </remarks>
/// <param name="onStateApplied">The action to run when the state is applied.</param>
/// <param name="onStateRemoved">The action to run when the state is removed; should undo the apply action.</param>
public sealed class StateBehavior(
    Action<Animatable>? onStateApplied = null,
    Action<Animatable>? onStateRemoved = null)
        : IStateBehavior
{
    /// <inheritdoc cref="IStateBehavior.OnStateApplied(Animatable)"/>
    public void OnStateApplied(Animatable target) => onStateApplied?.Invoke(target);

    /// <inheritdoc cref="IStateBehavior.OnStateRemoved(Animatable)"/>
    public void OnStateRemoved(Animatable target) => onStateRemoved?.Invoke(target);
}
