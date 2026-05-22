using ArqMover.Desktop.Core.Enums;
using ArqMover.Desktop.Core.Models;

namespace ArqMover.Desktop.Interface.ViewModels;

public class MainViewModel
{
    public string CaminhoOrigem { get; set; } = string.Empty;
    public string CaminhoDestino { get; set; } = string.Empty;
    public ModoOperacao Modo { get; set; } = ModoOperacao.Transferencia;
    public string Status { get; set; } = "Pronto para iniciar a transferência.";
    public string Log { get; set; } = string.Empty;

    public Transferencia CriarTransferencia()
    {
        return new Transferencia
        {
            CaminhoOrigem = this.CaminhoOrigem,
            CaminhoDestino = this.CaminhoDestino,
            Modo = this.Modo,
        };
    }
}
