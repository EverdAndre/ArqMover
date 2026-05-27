using System.ComponentModel;
using ArqMover.Desktop.Core.Enums;
using ArqMover.Desktop.Core.Models;
using ArqMover.Desktop.Core.Services.Interfaces;
using ArqMover.Desktop.Infrastructure.Logs;

namespace ArqMover.Desktop.Interface.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly IOperacaoService _operacaoService;
    private string _status = "Pronto para iniciar a transferência.";
    private string _log = string.Empty;
    private int _progresso;

    public event PropertyChangedEventHandler? PropertyChanged;

    private string _caminhoOrigem = string.Empty;
    private string _caminhoDestino = string.Empty;
    private ModoOperacao _modo = ModoOperacao.Transferencia;

    public string Status
    {
        get => _status;
        set
        {
            _status = value;
            OnPropertyChanged(nameof(Status));
        }
    }

    public string Log
    {
        get => _log;
        set
        {
            _log = value;
            OnPropertyChanged(nameof(Log));
        }
    }

    public string CaminhoOrigem
    {
        get => _caminhoOrigem;
        set
        {
            _caminhoOrigem = value;
            OnPropertyChanged(nameof(CaminhoOrigem));
        }
    }

    public string CaminhoDestino
    {
        get => _caminhoDestino;
        set
        {
            _caminhoDestino = value;
            OnPropertyChanged(nameof(CaminhoDestino));
        }
    }

    public ModoOperacao Modo
    {
        get => _modo;
        set
        {
            _modo = value;
            OnPropertyChanged(nameof(Modo));
        }
    }
    public int Progresso
    {
        get => _progresso;
        set
        {
            _progresso = value;
            OnPropertyChanged(nameof(Progresso));
        }
    }

    public MainViewModel(IOperacaoService operacaoService, LogService logService)
    {
        _operacaoService = operacaoService;
        logService.LogRegistrado += (mensagem) =>
        {
            Log += mensagem + Environment.NewLine;
        };
    }

    public Transferencia CriarTransferencia()
    {
        return new Transferencia
        {
            CaminhoOrigem = this.CaminhoOrigem,
            CaminhoDestino = this.CaminhoDestino,
            Modo = this.Modo,
        };
    }

    public void Executar()
    {
        try
        {
            Progresso = 0;
            Transferencia transferencia = CriarTransferencia();
            Progresso = 25;
            Status = "Executando operação...";
            Log += "Iniciando operação..." + Environment.NewLine;
            Progresso = 30;
            //Verifica modo de operação selecionado
            if (transferencia.Modo == ModoOperacao.Transferencia)
            {
                Log += "Modo de operação Transferência selecionado." + Environment.NewLine;
            }
            else
            {
                Log += "Gestão de Backups selecionada." + Environment.NewLine;
            }
            Progresso = 50;
            _operacaoService.Executar(transferencia);
            Progresso = 75;
            Status = "Operação concluída com sucesso.";
            Log += "Operação concluída com sucesso." + Environment.NewLine;
            Progresso = 100;
        }
        catch (Exception ex)
        {
            Status = "Erro na operação.";
            Log += $"Erro: {ex.Message}" + Environment.NewLine;
        }
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
