using System.ComponentModel;
using System.Windows;
using GameBoost.UI.ViewModels;
namespace GameBoost.UI;

public partial class MainWindow : Window
{
    private readonly MainViewModel viewModel = new();
    public MainWindow()
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += async (_, _) => await viewModel.InitializeAsync();
        Closing += OnClosing;
    }
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (viewModel.IsBusy) { e.Cancel = true; MessageBox.Show("Aguarde a operação terminar ou cancele a coleta de benchmark."); return; }
        if (viewModel.GameActive) { e.Cancel = true; MessageBox.Show("Mantenha o GameBoost aberto até o jogo encerrar para concluir o acompanhamento e a restauração. Você ainda pode usar Restaurar tudo."); return; }
        if (viewModel.HasPending && MessageBox.Show("Há ajustes ativos. Fechar mantém os ajustes no Windows; você poderá restaurá-los na próxima abertura. Fechar mesmo assim?", "Ajustes pendentes", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) e.Cancel = true;
    }
}
