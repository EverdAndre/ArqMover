using ArqMover.Desktop.Core.Enums;
using ArqMover.Desktop.Core.Models;
using ArqMover.Desktop.Core.Services.Interfaces;

namespace ArqMover.Desktop.Core.Services;

public class ModoOperacaoService : IOperacaoService
{
    private readonly TransferenciaService _transferenciaService;
    private readonly BackupService _backupService;

    public ModoOperacaoService(TransferenciaService transferenciaService, BackupService backupService)
    {
        _transferenciaService = transferenciaService;
        _backupService = backupService;
    }

    public Task ExecutarAsync(
        Transferencia transferencia,
        IProgress<int>? progresso = null,
        CancellationToken cancellationToken = default
    )
    {
        return EscolherService(transferencia).ExecutarAsync(transferencia, progresso, cancellationToken);
    }

    private IOperacaoService EscolherService(Transferencia transferencia)
    {
        return transferencia.Modo switch
        {
            ModoOperacao.Transferencia => _transferenciaService,
            ModoOperacao.Backup => _backupService,
            _ => throw new ArgumentOutOfRangeException(
                nameof(transferencia),
                "Modo de operacao invalido."
            ),
        };
    }
}
