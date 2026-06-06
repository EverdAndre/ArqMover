using System.IO;
using ArqMover.Desktop.Core.Models;
using ArqMover.Desktop.Core.Services.Interfaces;
using ArqMover.Desktop.Infrastructure.Logs;

namespace ArqMover.Desktop.Core.Services;

public class BackupService : IOperacaoService
{
    private readonly LogService _logService;
    private readonly TransferenciaService _transferenciaService;

    public BackupService(LogService logService, TransferenciaService transferenciaService)
    {
        _logService = logService;
        _transferenciaService = transferenciaService;
    }

    public Task ExecutarAsync(
        Transferencia transferencia,
        IProgress<int>? progresso,
        CancellationToken cancellationToken
    )
    {
        // backup em background para a segunda verificacao nao travar a UI.
        return Task.Run(
            () => ExecutarBackupAsync(transferencia, progresso, cancellationToken),
            cancellationToken
        );
    }

    private async Task ExecutarBackupAsync(
        Transferencia transferencia,
        IProgress<int>? progresso,
        CancellationToken cancellationToken
    )
    {
        ValidarBackup(transferencia);
        Directory.CreateDirectory(transferencia.CaminhoDestino);
        progresso?.Report(0);

        _logService.Registrar("Iniciando backup de arquivos.");
        _logService.Registrar($"Modo de operacao: {transferencia.Modo}");
        _logService.Registrar($"Origem: {transferencia.CaminhoOrigem}");
        _logService.Registrar($"Destino: {transferencia.CaminhoDestino}");

        cancellationToken.ThrowIfCancellationRequested();

        if (File.Exists(transferencia.CaminhoOrigem))
        {
            //arquivo unico em modo backup e tratado pela transferencia normal.
            _logService.Registrar("Executando transferencia normal para arquivo unico.");
            await _transferenciaService.ExecutarAsync(transferencia, progresso, cancellationToken);
            return;
        }

        string[] arquivosVbkOrigem = EnumerarArquivosSeguro(
                transferencia.CaminhoOrigem,
                "*.vbk",
                SearchOption.AllDirectories
            )
            .ToArray();
        string[] arquivosVibOrigem = EnumerarArquivosSeguro(
                transferencia.CaminhoOrigem,
                "*.vib",
                SearchOption.AllDirectories
            )
            .ToArray();
        string[] arquivosVbmOrigem = EnumerarArquivosSeguro(
                transferencia.CaminhoOrigem,
                "*.vbm",
                SearchOption.AllDirectories
            )
            .ToArray();
        string[] arquivosBcoOrigem = EnumerarArquivosSeguro(
                transferencia.CaminhoOrigem,
                "*.bco",
                SearchOption.AllDirectories
            )
            .ToArray();

        // calcula itens do backup para a barra atualizar durante a verificacao.
        int totalItensBackup =
            arquivosVbkOrigem.Length
            + arquivosVibOrigem.Length
            + arquivosVbmOrigem.Length
            + arquivosBcoOrigem.Length;
        int itensProcessados = 0;
        progresso?.Report(5);

        void ReportarItemProcessado()
        {
            itensProcessados++;
            progresso?.Report(CalcularProgressoEmFaixa(itensProcessados, totalItensBackup, 5, 95));
        }

        //usa a mesma pasta base criada pela transferencia normal: destino + nome da pasta origem.
        string destinoBaseBackup = MontarDestinoBaseBackup(transferencia);

        bool destinoJaTemBackupInicial =
            Directory.Exists(destinoBaseBackup)
            && EnumerarArquivosSeguro(destinoBaseBackup, "*.vbk", SearchOption.AllDirectories).Any();

        if (arquivosVbkOrigem.Length > 0 && !destinoJaTemBackupInicial)
        {
            _logService.Registrar(
                "Nenhum arquivo .vbk encontrado no destino. Executando transferencia normal para carga inicial."
            );
            await _transferenciaService.ExecutarAsync(transferencia, progresso, cancellationToken);
            return;
        }

        foreach (string arquivoVbk in arquivosVbkOrigem)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string caminhoVbkRelativo = Path.GetRelativePath(
                transferencia.CaminhoOrigem,
                arquivoVbk
            );
            string caminhoVbkDestino = Path.Combine(destinoBaseBackup, caminhoVbkRelativo);

            if (!File.Exists(caminhoVbkDestino))
            {
                _logService.Registrar($"Arquivo .vbk nao encontrado no destino: {caminhoVbkDestino}.");
                _logService.Registrar($"Iniciando copia do arquivo .vbk: {arquivoVbk}");
                CopiarArquivoComCancelamento(
                    arquivoVbk,
                    caminhoVbkDestino,
                    sobrescrever: false,
                    cancellationToken
                );
                _logService.Registrar($"Arquivo .vbk copiado: {arquivoVbk} -> {caminhoVbkDestino}");
            }
            else
            {
                FileInfo vbkOrigemInfo = new FileInfo(arquivoVbk);
                FileInfo vbkDestinoInfo = new FileInfo(caminhoVbkDestino);

                bool vbkIdentico =
                    vbkOrigemInfo.Name == vbkDestinoInfo.Name
                    && vbkOrigemInfo.Length == vbkDestinoInfo.Length;

                if (vbkIdentico)
                {
                    _logService.Registrar(
                        $"Backup ignorou o arquivo .vbk porque ja existe no destino: {caminhoVbkDestino}."
                    );
                }
                else if (vbkDestinoInfo.Length > vbkOrigemInfo.Length)
                {
                    _logService.Registrar(
                        $"Arquivo .vbk encontrado no destino e maior que o da origem: {caminhoVbkDestino}."
                    );
                }
                else
                {
                    _logService.Registrar(
                        $"Arquivo .vbk encontrado no destino e menor que o da origem: {caminhoVbkDestino}."
                    );
                }
            }

            // os .vib so sao avaliados depois que o .vbk correspondente ja existe no destino.
            CopiarVibsIncrementaisDoVbk(
                arquivoVbk,
                caminhoVbkDestino,
                cancellationToken,
                ReportarItemProcessado
            );

            // o .vbm da cadeia do .vbk e copiado para o destino relativo e pode sobrescrever.
            CopiarVbmsDoVbk(
                arquivoVbk,
                caminhoVbkDestino,
                cancellationToken,
                ReportarItemProcessado
            );

            ReportarItemProcessado();
        }

        //os .bco nao fazem parte da cadeia .vbk e sao incrementados pela propria pasta relativa.
        CopiarBcosIncrementais(
            transferencia,
            destinoBaseBackup,
            cancellationToken,
            ReportarItemProcessado
        );

        progresso?.Report(100);
        _logService.Registrar("Verificacao de backup concluida.");
    }

    private void CopiarVibsIncrementaisDoVbk(
        string arquivoVbkOrigem,
        string arquivoVbkDestino,
        CancellationToken cancellationToken,
        Action reportarItemProcessado
    )
    {
        string? pastaVbkOrigem = Path.GetDirectoryName(arquivoVbkOrigem);
        string? pastaVbkDestino = Path.GetDirectoryName(arquivoVbkDestino);

        if (string.IsNullOrWhiteSpace(pastaVbkOrigem) || string.IsNullOrWhiteSpace(pastaVbkDestino))
            return;

        Directory.CreateDirectory(pastaVbkDestino);

        // copia apenas os .vib da mesma pasta do .vbk, preservando a cadeia incremental do Veeam.
        string[] arquivosVibOrigem = EnumerarArquivosSeguro(
                pastaVbkOrigem,
                "*.vib",
                SearchOption.TopDirectoryOnly
            )
            .ToArray();

        foreach (string arquivoVib in arquivosVibOrigem)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string caminhoVibDestino = Path.Combine(pastaVbkDestino, Path.GetFileName(arquivoVib));
            FileInfo vibOrigemInfo = new FileInfo(arquivoVib);

            if (File.Exists(caminhoVibDestino))
            {
                FileInfo vibDestinoInfo = new FileInfo(caminhoVibDestino);

                bool vibIdentico =
                    vibOrigemInfo.Name == vibDestinoInfo.Name
                    && vibOrigemInfo.Length == vibDestinoInfo.Length;

                if (vibIdentico)
                {
                    //.vib igual na origem e no destino e ignorado.
                    _logService.Registrar(
                        $"Arquivo .vib ja existe no destino e e identico ao da origem: {caminhoVibDestino}"
                    );
                    reportarItemProcessado();
                    continue;
                }

                //.vib existente no destino nunca e sobrescrito.
                _logService.Registrar(
                    $"Arquivo .vib ja existe no destino. Backup nao sobrescreveu: {caminhoVibDestino}"
                );
                reportarItemProcessado();
                continue;
            }

            // .vib ausente no destino e acrescentado sem sobrescrever arquivo existente.
            _logService.Registrar($"Iniciando copia do arquivo .vib: {arquivoVib}");
            CopiarArquivoComCancelamento(
                arquivoVib,
                caminhoVibDestino,
                sobrescrever: false,
                cancellationToken
            );
            _logService.Registrar(
                $"Arquivo .vib incremental copiado: {arquivoVib} -> {caminhoVibDestino}"
            );
            reportarItemProcessado();
        }
    }

    private void CopiarVbmsDoVbk(
        string arquivoVbkOrigem,
        string arquivoVbkDestino,
        CancellationToken cancellationToken,
        Action reportarItemProcessado
    )
    {
        string? pastaVbkOrigem = Path.GetDirectoryName(arquivoVbkOrigem);
        string? pastaVbkDestino = Path.GetDirectoryName(arquivoVbkDestino);

        if (string.IsNullOrWhiteSpace(pastaVbkOrigem) || string.IsNullOrWhiteSpace(pastaVbkDestino))
            return;

        Directory.CreateDirectory(pastaVbkDestino);

        // avalia os .vbm da mesma pasta do .vbk porque eles sao metadados da cadeia Veeam.
        string[] arquivosVbmOrigem = EnumerarArquivosSeguro(
                pastaVbkOrigem,
                "*.vbm",
                SearchOption.TopDirectoryOnly
            )
            .ToArray();

        foreach (string arquivoVbm in arquivosVbmOrigem)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string caminhoVbmDestino = Path.Combine(pastaVbkDestino, Path.GetFileName(arquivoVbm));

            if (SaoOMesmoArquivo(arquivoVbm, caminhoVbmDestino))
            {
                // evita copiar .vbm para ele mesmo quando origem ja esta dentro do destino.
                _logService.Registrar(
                    $"Arquivo .vbm ja esta no destino relativo. Backup ignorou copia para si mesmo: {caminhoVbmDestino}"
                );
                reportarItemProcessado();
                continue;
            }

            // .vbm pode sobrescrever no destino relativo para manter metadados atualizados.
            CopiarArquivoComCancelamento(
                arquivoVbm,
                caminhoVbmDestino,
                sobrescrever: true,
                cancellationToken
            );
            _logService.Registrar(
                $"Arquivo .vbm copiado/atualizado: {arquivoVbm} -> {caminhoVbmDestino}"
            );
            reportarItemProcessado();
        }
    }

    private void CopiarBcosIncrementais(
        Transferencia transferencia,
        string destinoBaseBackup,
        CancellationToken cancellationToken,
        Action reportarItemProcessado
    )
    {
        // busca .bco em toda a origem porque eles possuem estrutura propria, separada do .vbk.
        string[] arquivosBcoOrigem = EnumerarArquivosSeguro(
                transferencia.CaminhoOrigem,
                "*.bco",
                SearchOption.AllDirectories
            )
            .ToArray();

        foreach (string arquivoBco in arquivosBcoOrigem)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string caminhoBcoRelativo = Path.GetRelativePath(
                transferencia.CaminhoOrigem,
                arquivoBco
            );
            string caminhoBcoDestino = Path.Combine(destinoBaseBackup, caminhoBcoRelativo);
            string? pastaBcoDestino = Path.GetDirectoryName(caminhoBcoDestino);

            if (string.IsNullOrWhiteSpace(pastaBcoDestino))
                continue;

            Directory.CreateDirectory(pastaBcoDestino);

            FileInfo bcoOrigemInfo = new FileInfo(arquivoBco);

            if (File.Exists(caminhoBcoDestino))
            {
                FileInfo bcoDestinoInfo = new FileInfo(caminhoBcoDestino);

                bool bcoIdentico =
                    bcoOrigemInfo.Name == bcoDestinoInfo.Name
                    && bcoOrigemInfo.Length == bcoDestinoInfo.Length;

                if (bcoIdentico)
                {
                    //.bco igual na origem e no destino e ignorado.
                    _logService.Registrar(
                        $"Arquivo .bco ja existe no destino e e identico ao da origem: {caminhoBcoDestino}"
                    );
                    reportarItemProcessado();
                    continue;
                }

                // .bco existente no destino nunca e sobrescrito.
                _logService.Registrar(
                    $"Arquivo .bco ja existe no destino. Backup nao sobrescreveu: {caminhoBcoDestino}"
                );
                reportarItemProcessado();
                continue;
            }

            //.bco ausente e acrescentado na pasta relativa sem sobrescrever.
            CopiarArquivoComCancelamento(
                arquivoBco,
                caminhoBcoDestino,
                sobrescrever: false,
                cancellationToken
            );
            _logService.Registrar($"Arquivo .bco copiado: {arquivoBco} -> {caminhoBcoDestino}");
            reportarItemProcessado();
        }
    }

    private static string MontarDestinoBaseBackup(Transferencia transferencia)
    {
        //raiz de disco nao pode usar DirectoryInfo.Name porque retorna "G:\" e sobrescreve o destino no Path.Combine.
        string caminhoOrigem = Path.GetFullPath(transferencia.CaminhoOrigem);
        string raizOrigem = Path.GetPathRoot(caminhoOrigem) ?? string.Empty;

        if (string.Equals(caminhoOrigem, raizOrigem, StringComparison.OrdinalIgnoreCase))
            return transferencia.CaminhoDestino;

        //replica a regra do TransferenciaService para comparar no caminho onde os arquivos realmente ficam.
        string nomePastaOrigem = Path.GetFileName(
            caminhoOrigem.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
        );
        return Path.Combine(transferencia.CaminhoDestino, nomePastaOrigem);
    }

    private static bool SaoOMesmoArquivo(string caminhoOrigem, string caminhoDestino)
    {
        // compara caminhos completos para evitar File.Copy no mesmo arquivo.
        string origemCompleta = Path.GetFullPath(caminhoOrigem);
        string destinoCompleto = Path.GetFullPath(caminhoDestino);
        return string.Equals(origemCompleta, destinoCompleto, StringComparison.OrdinalIgnoreCase);
    }

    private static int CalcularProgressoEmFaixa(
        int itensProcessados,
        int totalItens,
        int inicio,
        int fim
    )
    {
        // itens processados em percentual parcial para a barra nao ficar parada.
        if (totalItens <= 0)
            return fim;

        int progressoCalculado =
            inicio + (int)Math.Round(itensProcessados * (fim - inicio) / (double)totalItens);

        return Math.Clamp(progressoCalculado, inicio, fim);
    }

    private static void CopiarArquivoComCancelamento(
        string arquivoOrigem,
        string arquivoDestino,
        bool sobrescrever,
        CancellationToken cancellationToken
    )
    {
        // copia em blocos para o cancelamento parar durante arquivos grandes.;
        cancellationToken.ThrowIfCancellationRequested();

        string destinoFinal = arquivoDestino;
        string destinoEscrita = arquivoDestino;
        string? pastaDestino = Path.GetDirectoryName(arquivoDestino);

        if (!string.IsNullOrWhiteSpace(pastaDestino))
            Directory.CreateDirectory(pastaDestino);

        if (sobrescrever)
            destinoEscrita = arquivoDestino + ".tmp";

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
                destinoEscrita,
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

            if (sobrescrever)
            {
                destino.Dispose();
                File.Move(destinoEscrita, destinoFinal, true);
            }
        }
        catch (OperationCanceledException)
        {
            if (File.Exists(destinoEscrita) && !SaoOMesmoArquivo(destinoEscrita, destinoFinal))
                File.Delete(destinoEscrita);
            else if (!sobrescrever && File.Exists(destinoFinal))
                File.Delete(destinoFinal);

            throw;
        }
    }

    private IEnumerable<string> EnumerarArquivosSeguro(
        string pastaOrigem,
        string filtro,
        SearchOption searchOption
    )
    {
        //ignora pastas sem permissao, como System Volume Information, sem derrubar o backup.
        var opcoes = new EnumerationOptions
        {
            RecurseSubdirectories = searchOption == SearchOption.AllDirectories,
            IgnoreInaccessible = true,
            ReturnSpecialDirectories = false,
        };

        try
        {
            return Directory.EnumerateFiles(pastaOrigem, filtro, opcoes).ToArray();
        }
        catch (UnauthorizedAccessException ex)
        {
            _logService.Registrar(
                $"Pasta sem permissao ignorada: {pastaOrigem}. Detalhe: {ex.Message}"
            );
            return Array.Empty<string>();
        }
    }

    private void ValidarBackup(Transferencia transferencia)
    {
        if (string.IsNullOrWhiteSpace(transferencia.CaminhoOrigem))
            throw new ArgumentException("O caminho de origem nao pode estar vazio.");

        if (string.IsNullOrWhiteSpace(transferencia.CaminhoDestino))
            throw new ArgumentException("O caminho de destino nao pode estar vazio.");

        if (
            !Directory.Exists(transferencia.CaminhoOrigem)
            && !File.Exists(transferencia.CaminhoOrigem)
        )
            throw new DirectoryNotFoundException("A pasta de origem nao foi encontrada.");
    }
}
