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

    // executa a copia e envia progresso real.
    public Task ExecutarAsync(
        Transferencia transferencia,
        IProgress<int>? progresso = null,
        CancellationToken cancellationToken = default
    )
    {
        return Task.Run(
            () => ExecutarTransferencia(transferencia, progresso, cancellationToken),
            cancellationToken
        );
    }

    private void ExecutarTransferencia(
        Transferencia transferencia,
        IProgress<int>? progresso,
        CancellationToken cancellationToken
    )
    {
        ValidarTransferencia(transferencia);

        Directory.CreateDirectory(transferencia.CaminhoDestino);
        progresso?.Report(0);

        _logService.Registrar("Iniciando transferencia de arquivos.");
        _logService.Registrar($"Modo de operacao: {transferencia.Modo}");
        _logService.Registrar($"Origem: {transferencia.CaminhoOrigem}");
        _logService.Registrar($"Destino: {transferencia.CaminhoDestino}");

        if (Directory.Exists(transferencia.CaminhoOrigem))
        {
            ExecutarTransferenciaNormal(transferencia, progresso, cancellationToken);
            _logService.Registrar("Transferencia normal concluida.");
        }
        else if (File.Exists(transferencia.CaminhoOrigem))
        {
            ExecutarTransferenciaArquivo(transferencia, progresso, cancellationToken);
            _logService.Registrar("Transferencia de arquivo concluida.");
        }
        else
        {
            _logService.Registrar("Caminho de origem nao encontrado.");
            throw new FileNotFoundException("O caminho de origem nao foi encontrado.");
        }
    }

    private void ValidarTransferencia(Transferencia transferencia)
    {
        if (string.IsNullOrWhiteSpace(transferencia.CaminhoOrigem))
            throw new ArgumentException("O caminho de origem nao pode estar vazio.");

        if (string.IsNullOrWhiteSpace(transferencia.CaminhoDestino))
            throw new ArgumentException("O caminho de destino nao pode estar vazio.");

        bool origemEhPasta = Directory.Exists(transferencia.CaminhoOrigem);
        bool origemEhArquivo = File.Exists(transferencia.CaminhoOrigem);

        if (!origemEhPasta && !origemEhArquivo)
            throw new FileNotFoundException("O caminho de origem nao foi encontrado.");
    }

    private void ExecutarTransferenciaNormal(
        Transferencia transferencia,
        IProgress<int>? progresso,
        CancellationToken cancellationToken
    )
    {
        try
        {
            _logService.Registrar("Executando transferencia normal...");

            // adicionei aqui 29/05/2026 - trata origem como raiz ou pasta normal sem quebrar o destino.
            string novoDestinoBase = MontarDestinoBaseTransferencia(transferencia);
            // materializa a lista para calcular percentual por arquivo processado.
            List<string> arquivosOrigem = EnumerarArquivosSeguro(transferencia.CaminhoOrigem)
                .ToList();

            if (arquivosOrigem.Count == 0)
            {
                progresso?.Report(100);
                _logService.Registrar("Nenhum arquivo encontrado na origem.");
                return;
            }

            int arquivosProcessados = 0;
            progresso?.Report(5);

            foreach (string arquivoOrigem in arquivosOrigem)
            {
                // permite cancelar futuramente sem travar a operacao.
                cancellationToken.ThrowIfCancellationRequested();

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
                    _logService.Registrar($"Arquivo ja existe no destino: {arquivoDestino}");
                }
                else
                {
                    CopiarArquivoComCancelamento(
                        arquivoOrigem,
                        arquivoDestino,
                        sobrescrever: false,
                        cancellationToken
                    );
                    _logService.Registrar($"Arquivo copiado: {arquivoOrigem} -> {arquivoDestino}");
                }

                arquivosProcessados++;
                // atualiza a barra no mesmo padrao do backup, deixando o fechamento para 100.
                progresso?.Report(
                    CalcularProgressoEmFaixa(arquivosProcessados, arquivosOrigem.Count, 5, 95)
                );
            }

            progresso?.Report(100);
        }
        catch (Exception ex)
        {
            _logService.Registrar($"Erro durante transferencia: {ex.Message}");
            throw;
        }
    }

    private void ExecutarTransferenciaArquivo(
        Transferencia transferencia,
        IProgress<int>? progresso,
        CancellationToken cancellationToken
    )
    {
        try
        {
            // mesmo arquivo unico respeita cancelamento e progresso.
            cancellationToken.ThrowIfCancellationRequested();
            _logService.Registrar("Executando transferencia de arquivo...");

            string nomeArquivoOrigem = Path.GetFileName(transferencia.CaminhoOrigem);
            string arquivoDestino = Path.Combine(transferencia.CaminhoDestino, nomeArquivoOrigem);

            if (File.Exists(arquivoDestino))
            {
                _logService.Registrar($"Arquivo ja existe no destino: {arquivoDestino}");
            }
            else
            {
                CopiarArquivoComCancelamento(
                    transferencia.CaminhoOrigem,
                    arquivoDestino,
                    sobrescrever: false,
                    cancellationToken
                );
                _logService.Registrar(
                    $"Arquivo copiado: {transferencia.CaminhoOrigem} -> {arquivoDestino}"
                );
            }

            progresso?.Report(100);
        }
        catch (Exception ex)
        {
            _logService.Registrar($"Erro durante transferencia: {ex.Message}");
            throw;
        }
    }

    private static int CalcularProgressoEmFaixa(
        int arquivosProcessados,
        int totalArquivos,
        int inicio,
        int fim
    )
    {
        if (totalArquivos <= 0)
            return fim;

        int progressoCalculado =
            inicio + (int)Math.Round(arquivosProcessados * (fim - inicio) / (double)totalArquivos);

        return Math.Clamp(progressoCalculado, inicio, fim);
    }

    private IEnumerable<string> EnumerarArquivosSeguro(string pastaOrigem)
    {
        // adicionei aqui 29/05/2026 - ignora pastas sem permissao para transferencias iniciadas pela raiz do disco.
        var opcoes = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            ReturnSpecialDirectories = false,
        };

        try
        {
            return Directory.EnumerateFiles(pastaOrigem, "*", opcoes);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logService.Registrar($"Pasta sem permissao ignorada: {pastaOrigem}. Detalhe: {ex.Message}");
            return Array.Empty<string>();
        }
    }

    private static string MontarDestinoBaseTransferencia(Transferencia transferencia)
    {
        // adicionei aqui 29/05/2026 - se a origem for raiz, copia direto dentro do destino escolhido.
        string caminhoOrigem = Path.GetFullPath(transferencia.CaminhoOrigem);
        string raizOrigem = Path.GetPathRoot(caminhoOrigem) ?? string.Empty;

        if (string.Equals(caminhoOrigem, raizOrigem, StringComparison.OrdinalIgnoreCase))
            return transferencia.CaminhoDestino;

        string nomePastaOrigem = Path.GetFileName(caminhoOrigem.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar
        ));

        return Path.Combine(transferencia.CaminhoDestino, nomePastaOrigem);
    }

    private static void CopiarArquivoComCancelamento(
        string arquivoOrigem,
        string arquivoDestino,
        bool sobrescrever,
        CancellationToken cancellationToken
    )
    {
        // adicionei aqui 29/05/2026 - copia em blocos para o botao cancelar interromper arquivos grandes.
        cancellationToken.ThrowIfCancellationRequested();

        string? pastaDestino = Path.GetDirectoryName(arquivoDestino);
        if (!string.IsNullOrWhiteSpace(pastaDestino))
            Directory.CreateDirectory(pastaDestino);

        FileMode modoDestino = sobrescrever ? FileMode.Create : FileMode.CreateNew;
        byte[] buffer = new byte[1024 * 1024];

        try
        {
            using FileStream origem = new FileStream(
                arquivoOrigem,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                buffer.Length
            );
            using FileStream destino = new FileStream(
                arquivoDestino,
                modoDestino,
                FileAccess.Write,
                FileShare.None,
                buffer.Length
            );

            int bytesLidos;
            while ((bytesLidos = origem.Read(buffer, 0, buffer.Length)) > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                destino.Write(buffer, 0, bytesLidos);
            }
        }
        catch (OperationCanceledException)
        {
            if (!sobrescrever && File.Exists(arquivoDestino))
                File.Delete(arquivoDestino);

            throw;
        }
    }
}
