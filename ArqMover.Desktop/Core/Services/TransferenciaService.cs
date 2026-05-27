using System.IO;
using ArqMover.Desktop.Core.Models;
using ArqMover.Desktop.Core.Services.Interfaces;
using ArqMover.Desktop.Infrastructure.Logs;

namespace ArqMover.Desktop.Core.Services;

public class TransferenciaService : IOperacaoService
{
    private readonly LogService _logService;

    public TransferenciaService(LogService logService)
    {
        _logService = logService;
    }

    // registra log de validação
    public void Executar(Transferencia transferencia)
    {
        ValidarTransferencia(transferencia);

        Directory.CreateDirectory(transferencia.CaminhoDestino);

        _logService.Registrar("Iniciando transferência de arquivos.");
        _logService.Registrar($"Modo de operação: {transferencia.Modo}");
        _logService.Registrar($"Origem: {transferencia.CaminhoOrigem}");
        _logService.Registrar($"Destino: {transferencia.CaminhoDestino}");
        // Chama transferência normal ou Arquivo
        if (Directory.Exists(transferencia.CaminhoOrigem))
        {
            ExecutarTransferenciaNormal(transferencia);
            _logService.Registrar("Transferência Normal concluída.");
        }
        else if (File.Exists(transferencia.CaminhoOrigem))
        {
            ExecutarTransferenciaArquivo(transferencia);
            _logService.Registrar("Transferência de Arquivo concluída.");
        }
        else
        {
            _logService.Registrar("Caminho de origem não encontrado.");
            throw new FileNotFoundException("O caminho de origem não foi encontrado.");
        }

    }

    //METODOS
    // validar os caminhos de origem e destino
    private void ValidarTransferencia(Transferencia transferencia)
    {
        if (string.IsNullOrWhiteSpace(transferencia.CaminhoOrigem))
            throw new ArgumentException("O caminho de origem não pode estar vazio.");

        if (string.IsNullOrWhiteSpace(transferencia.CaminhoDestino))
            throw new ArgumentException("O caminho de destino não pode estar vazio.");

        bool origemEhPasta = Directory.Exists(transferencia.CaminhoOrigem);
        bool origemEhArquivo = File.Exists(transferencia.CaminhoOrigem);

        if (!origemEhPasta && !origemEhArquivo)
            throw new FileNotFoundException("O caminho de origem não foi encontrado.");

    }

    // Executa transferencia normal de pasta
    private void ExecutarTransferenciaNormal(Transferencia transferencia)
    {
        try
        {
            _logService.Registrar("Executando transferência normal...");

            string nomePastaOrigem = Path.GetFileName(transferencia.CaminhoOrigem);

            string novoDestinoBase = Path.Combine(transferencia.CaminhoDestino, nomePastaOrigem);

            foreach (
                string arquivoOrigem in Directory.EnumerateFiles(
                    transferencia.CaminhoOrigem,
                    "*",
                    SearchOption.AllDirectories
                )
            )
            {
                string caminhoRelativo = Path.GetRelativePath(
                    transferencia.CaminhoOrigem,
                    arquivoOrigem
                );

                string arquivoDestino = Path.Combine(novoDestinoBase, caminhoRelativo);

                string? pastaDestino = Path.GetDirectoryName(arquivoDestino);

                if (!string.IsNullOrWhiteSpace(pastaDestino))
                {
                    Directory.CreateDirectory(pastaDestino);
                }
                _logService.Registrar($"Destino montado: {arquivoDestino}");

                if (File.Exists(arquivoDestino))
                {
                    _logService.Registrar($"Arquivo já existe no destino: {arquivoDestino}");
                    continue;
                }
                else
                {
                    File.Copy(arquivoOrigem, arquivoDestino);
                    _logService.Registrar($"Arquivo copiado: {arquivoOrigem} -> {arquivoDestino}");
                }
            }
        }
        catch (Exception ex)
        {
            _logService.Registrar($"Erro durante transferência: {ex.Message}");
            throw;
        }
    }
    private void ExecutarTransferenciaArquivo(Transferencia transferencia)
    {
        try
        {
            _logService.Registrar("Executando transferência de arquivo...");

            string nomeArquivoOrigem = Path.GetFileName(transferencia.CaminhoOrigem);
            string arquivoDestino = Path.Combine(transferencia.CaminhoDestino, nomeArquivoOrigem);

            if (File.Exists(arquivoDestino))
            {
                _logService.Registrar($"Arquivo já existe no destino: {arquivoDestino}");

            }
            else
            {
                File.Copy(transferencia.CaminhoOrigem, arquivoDestino);
                _logService.Registrar($"Arquivo copiado: {transferencia.CaminhoOrigem} -> {arquivoDestino}");
            }
        }
        catch (Exception ex)
        {
            _logService.Registrar($"Erro durante transferência: {ex.Message}");
            throw;
        }
    }
}
