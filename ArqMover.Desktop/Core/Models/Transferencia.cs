using System.ComponentModel.DataAnnotations;
using ArqMover.Desktop.Core.Enums;

namespace ArqMover.Desktop.Core.Models;

public class Transferencia
{
    [Required]
    public string CaminhoOrigem { get; set; } = string.Empty;

    [Required]
    public string CaminhoDestino { get; set; } = string.Empty;

    public ModoOperacao Modo { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.Now;
}
