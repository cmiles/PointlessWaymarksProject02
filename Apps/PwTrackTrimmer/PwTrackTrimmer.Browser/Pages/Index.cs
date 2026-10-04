using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using System.Threading.Tasks;
using OpenSilver.WebAssembly;

namespace PwTrackTrimmer.Browser.Pages
{
    [Route("/")]
    [Route("/index.html")]
    [Route("/index.htm")]
    public class Index : ComponentBase
    {
        protected override void BuildRenderTree(RenderTreeBuilder __builder)
        {
        }

        protected async override Task OnInitializedAsync()
        {
            await base.OnInitializedAsync();
            await Runner.RunApplicationAsync<PwTrackTrimmer.App>();
        }
    }
}