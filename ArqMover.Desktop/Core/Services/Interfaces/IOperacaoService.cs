using ArqMover.Desktop.Core.Models;

namespace ArqMover.Desktop.Core.Services.Interfaces;

public interface IOperacaoService
{
    void Executar(Transferencia transferencia);
}
