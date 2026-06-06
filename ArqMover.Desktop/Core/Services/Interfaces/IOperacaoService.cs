using ArqMover.Desktop.Core.Models;

namespace ArqMover.Desktop.Core.Services.Interfaces;

public interface IOperacaoService
{
    Task ExecutarAsync(
        Transferencia transferencia,
        IProgress<int>? progresso = null,
        CancellationToken cancellationToken = default
    );
}
