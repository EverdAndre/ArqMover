using System.IO;

namespace ArqMover.Desktop.Core.Services.Dialogs;

public class FolderDialogService
{
    private const string SelecionarPastaAtual = "Selecionar esta pasta";

    public static string ProcurarDestino()
    {
        var caminho = new FolderBrowserDialog();
        caminho.Description = "Selecione a pasta de destino";
        caminho.ShowDialog();
        return caminho.SelectedPath;
    }

    public static string ProcurarOrigem()
    {
        var caminho = new OpenFileDialog();
        caminho.CheckFileExists = false;
        caminho.CheckPathExists = true;
        caminho.FileName = SelecionarPastaAtual;
        caminho.Multiselect = false;
        caminho.Title = "Selecione um arquivo ou a pasta atual";
        caminho.ValidateNames = false;

        if (caminho.ShowDialog() != DialogResult.OK)
            return string.Empty;

        if (File.Exists(caminho.FileName) || Directory.Exists(caminho.FileName))
            return caminho.FileName;

        string? pastaSelecionada = Path.GetDirectoryName(caminho.FileName);

        return !string.IsNullOrWhiteSpace(pastaSelecionada) && Directory.Exists(pastaSelecionada)
            ? pastaSelecionada
            : string.Empty;
    }
}
