using System.Diagnostics;
using System.Windows;
using ArqMover.Desktop.Core.Enums;
using ArqMover.Desktop.Core.Services;
using ArqMover.Desktop.Core.Services.Dialogs;
using ArqMover.Desktop.Infrastructure.Logs;
using ArqMover.Desktop.Interface.ViewModels;

namespace ArqMover.Desktop.Interface.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _mainViewModel;
    private readonly LogService _logService;

    public MainWindow()
    {
        _logService = new LogService();
        var transferenciaService = new TransferenciaService(_logService);
        var backupService = new BackupService(_logService, transferenciaService);
        var operacaoService = new ModoOperacaoService(transferenciaService, backupService);

        _mainViewModel = new MainViewModel(operacaoService, _logService);

        InitializeComponent();

        DataContext = _mainViewModel;
    }

    private void Transferencia_Checked(object sender, RoutedEventArgs e)
    {
        _mainViewModel.Modo = ModoOperacao.Transferencia;
    }

    private void Backups_Checked(object sender, RoutedEventArgs e)
    {
        _mainViewModel.Modo = ModoOperacao.Backup;
    }

    private void ProcurarOrigem_Click(object sender, RoutedEventArgs e)
    {
        string caminhoSelecionado = FolderDialogService.ProcurarOrigem();

        if (string.IsNullOrWhiteSpace(caminhoSelecionado))
            return;

        _mainViewModel.CaminhoOrigem = caminhoSelecionado;
    }

    private void ProcurarDestino_Click(object sender, RoutedEventArgs e)
    {
        string caminhoSelecionado = FolderDialogService.ProcurarDestino();

        if (string.IsNullOrWhiteSpace(caminhoSelecionado))
            return;

        _mainViewModel.CaminhoDestino = caminhoSelecionado;
    }

    private async void IniciarOperacao_Click(object sender, RoutedEventArgs e)
    {
        await _mainViewModel.ExecutarAsync();
    }

    private void CancelarOperacao_Click(object sender, RoutedEventArgs e)
    {
        // encaminha o cancelamento solicitado pelo usuario para a ViewModel.
        _mainViewModel.CancelarOperacao();
    }

    private void MostrarLogs_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(
            new ProcessStartInfo { FileName = _logService.PastaLogs, UseShellExecute = true }
        );
    }
}
