using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
namespace GameBoost.UI.ViewModels;

public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
    protected void Set<T>(ref T field, T value, [CallerMemberName] string? name = null) { field = value; Changed(name); }
}
public sealed class AsyncCommand(Func<Task> execute, Func<bool>? canExecute = null) : ICommand
{
    private bool running;
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => !running && (canExecute?.Invoke() ?? true);
    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter)) return;
        running = true; Refresh();
        try { await execute(); } finally { running = false; Refresh(); }
    }
    public void Refresh() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
public sealed class OptimizationOption(GameBoost.Core.OptimizationInfo info) : ObservableObject
{
    public GameBoost.Core.OptimizationInfo Info => info;
    private bool selected;
    public bool Selected { get => selected; set => Set(ref selected, value); }
    public string RiskLabel => info.Risk == GameBoost.Core.RiskLevel.Low ? "Baixo risco" : "Opcional · confira o impacto";
}
