using Radial.Models;

namespace Radial.Core;

public abstract record RadialAction(string Name) { public abstract Task ExecuteAsync(CancellationToken cancellationToken = default); }
public sealed record MacroAction(Macro Macro, MacroPlayer Player) : RadialAction(Macro.Name)
{ public override Task ExecuteAsync(CancellationToken cancellationToken = default) => Player.PlayAsync(Macro, cancellationToken); }
public sealed class ActionExecutor { public Task ExecuteAsync(RadialAction action, CancellationToken cancellationToken = default) => action.ExecuteAsync(cancellationToken); }
