using System.ComponentModel;
using System.Threading;
using ArqMover.Desktop.Core.Enums;
using ArqMover.Desktop.Core.Models;
using ArqMover.Desktop.Core.Services.Interfaces;
using ArqMover.Desktop.Infrastructure.Logs;

namespace ArqMover.Desktop.Interface.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly IOperacaoService _operacaoService;
    private readonly LogService _logService;

    // guarda o contexto da UI para atualizar Log com seguranca.
    private readonly SynchronizationContext? _synchronizationContext;
    private string _status = "Pronto para iniciar a transferencia.";
    private string _log = string.Empty;
    private int _progresso;

    // controla se ja existe uma operacao rodando.
    private bool _isExecutando;
    private CancellationTokenSource? _cancellationTokenSource;

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

    public bool IsExecutando
    {
        get => _isExecutando;
        set
        {
            _isExecutando = value;
            OnPropertyChanged(nameof(IsExecutando));
            OnPropertyChanged(nameof(PodeExecutar));
            OnPropertyChanged(nameof(PodeCancelar));
        }
    }

    // usado pelo XAML para desabilitar o botao Executar durante a copia.
    public bool PodeExecutar => !IsExecutando;
    public bool PodeCancelar => IsExecutando;

    public MainViewModel(IOperacaoService operacaoService, LogService logService)
    {
        _operacaoService = operacaoService;
        _logService = logService;
        _synchronizationContext = SynchronizationContext.Current;
        logService.LogRegistrado += mensagem =>
        {
            // logs podem vir da thread em background.
            AdicionarLogSeguro(mensagem);
        };
    }

    public Transferencia CriarTransferencia()
    {
        return new Transferencia
        {
            CaminhoOrigem = CaminhoOrigem,
            CaminhoDestino = CaminhoDestino,
            Modo = Modo,
        };
    }

    private void ValidarCaminhos(Transferencia transferencia)
    {
        if (string.IsNullOrWhiteSpace(transferencia.CaminhoOrigem))
            throw new ArgumentException("O caminho de origem nao pode estar vazio.");

        if (string.IsNullOrWhiteSpace(transferencia.CaminhoDestino))
            throw new ArgumentException("O caminho de destino nao pode estar vazio.");
    }

    // usa async/await para nao travar a UI.
    public async Task ExecutarAsync()
    {
        if (IsExecutando)
            return;

        try
        {
            IsExecutando = true;
            Progresso = 0;
            // permitir cancelar a operacao atual.
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();

            Transferencia transferencia = CriarTransferencia();
            ValidarCaminhos(transferencia);

            Status = "Executando operacao...";
            AdicionarLog("Iniciando operacao...");

            if (transferencia.Modo == ModoOperacao.Transferencia)
            {
                AdicionarLog("Modo de operacao Transferencia selecionado.");
            }
            else
            {
                AdicionarLog("Gestao de Backups selecionada.");
            }

            var progresso = new Progress<int>(valor => Progresso = valor);
            await _operacaoService.ExecutarAsync(
                transferencia,
                progresso,
                _cancellationTokenSource.Token
            );

            Status = "Operacao concluida com sucesso.";
            AdicionarLog("Operacao concluida com sucesso.");
            Progresso = 100;
        }
        catch (OperationCanceledException)
        {
            //registro cancelamento solicitado pelo usuario.
            Status = "Operacao cancelada pelo usuario.";
            _logService.Registrar("Operacao cancelada pelo usuario.");
        }
        catch (Exception ex)
        {
            Status = "Erro na operacao.";
            AdicionarLog($"Erro: {ex.Message}");
        }
        finally
        {
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
            IsExecutando = false;
        }
    }

    public void CancelarOperacao()
    {
        if (!IsExecutando || _cancellationTokenSource is null)
            return;

        //dispara o cancelamento solicitado pelo usuario.
        Status = "Cancelando operacao...";
        _logService.Registrar("Cancelamento solicitado pelo usuario.");
        _cancellationTokenSource.Cancel();
    }

    private void AdicionarLogSeguro(string mensagem)
    {
        if (_synchronizationContext is null)
        {
            AdicionarLog(mensagem);
            return;
        }

        _synchronizationContext.Post(_ => AdicionarLog(mensagem), null);
    }

    private void AdicionarLog(string mensagem)
    {
        Log += mensagem + Environment.NewLine;
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
