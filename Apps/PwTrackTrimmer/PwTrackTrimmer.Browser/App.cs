using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.CompilerServices;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Routing;

namespace PwTrackTrimmer.Browser
{
    public class App : ComponentBase
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<Router>(0);
            builder.AddAttribute(1, "AppAssembly", RuntimeHelpers.TypeCheck(
                typeof(Program).Assembly
            ));
            builder.AddAttribute(2, "PreferExactMatches", RuntimeHelpers.TypeCheck(
                true
            ));
            builder.AddAttribute(3, "Found", (RenderFragment<RouteData>)(routeData => builder2 =>
            {
                builder2.OpenComponent<RouteView>(4);
                builder2.AddAttribute(5, "RouteData", RuntimeHelpers.TypeCheck(
                    routeData
                ));
                builder2.CloseComponent();
            }
                ));
            builder.AddAttribute(7, "NotFound", (RenderFragment)(builder2 =>
            {
                builder2.OpenComponent<Pages.Index>(8);
                builder2.CloseComponent();
            }
                ));
            builder.CloseComponent();
        }
    }
}