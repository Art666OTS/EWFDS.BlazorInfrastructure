using Microsoft.AspNetCore.Components.Server.Circuits;

namespace EWFDS.BlazorInfrastructure.Services.Blazor
{
    /// <summary>
    /// Tracks the SignalR circuit connection state for Blazor Server.
    /// Useful for environments with intermittent connectivity (e.g., warehouses, mobile).
    /// </summary>
    public class CircuitHandlerService : CircuitHandler
    {
        public event EventHandler<bool>? CircuitStateChanged;

        public bool IsConnected { get; private set; } = true;

        public override Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
        {
            IsConnected = true;
            CircuitStateChanged?.Invoke(this, true);
            return base.OnCircuitOpenedAsync(circuit, cancellationToken);
        }

        public override Task OnConnectionUpAsync(Circuit circuit, CancellationToken cancellationToken)
        {
            IsConnected = true;
            CircuitStateChanged?.Invoke(this, true);
            return base.OnConnectionUpAsync(circuit, cancellationToken);
        }

        public override Task OnConnectionDownAsync(Circuit circuit, CancellationToken cancellationToken)
        {
            IsConnected = false;
            CircuitStateChanged?.Invoke(this, false);
            return base.OnConnectionDownAsync(circuit, cancellationToken);
        }

        public override Task OnCircuitClosedAsync(Circuit circuit, CancellationToken cancellationToken)
        {
            IsConnected = false;
            CircuitStateChanged?.Invoke(this, false);
            return base.OnCircuitClosedAsync(circuit, cancellationToken);
        }
    }
}
