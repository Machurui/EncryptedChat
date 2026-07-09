using Microsoft.JSInterop;

namespace tailwind_4_blazor_starter.Services;

public interface IFlowbiteService
{
    ValueTask InitializeFlowbiteAsync();
}

public class FlowbiteService(IJSRuntime jsRuntime) : IFlowbiteService
{
    private readonly IJSRuntime _jsRuntime = jsRuntime;

    public async ValueTask InitializeFlowbiteAsync()
    {
        await _jsRuntime.InvokeVoidAsync("flowbiteInterop.initializeFlowbite");
    }
}