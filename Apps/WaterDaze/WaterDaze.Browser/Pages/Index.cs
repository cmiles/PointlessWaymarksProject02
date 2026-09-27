using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using System.Threading.Tasks;
using OpenSilver.WebAssembly;

namespace WaterDaze.Browser.Pages
{
    [Route("/")]
    [Route("/{*path}")]
    public class Index : ComponentBase
    {
        [Parameter]
        public string Path { get; set; } = "";

        protected override void BuildRenderTree(RenderTreeBuilder __builder)
        {
        }

        protected async override Task OnInitializedAsync()
        {
            await base.OnInitializedAsync();
            await Runner.RunApplicationAsync<WaterDaze.App>();
        }
    }
}