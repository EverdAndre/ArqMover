using System.IO;

namespace ArqMover.Desktop.Infrastructure.Logs;

public class LogService
{
    private readonly string _pastaLogs;
    public string PastaLogs => _pastaLogs;
    private readonly string _arquivoLog;
    public event Action<string>? LogRegistrado;
    public LogService()
    {
        _pastaLogs = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
        Directory.CreateDirectory(_pastaLogs);
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        _arquivoLog = Path.Combine(_pastaLogs, $"{timestamp}.log");

    }
    public void Registrar(string mensagem)
    {
        string linhaLog = $"{DateTime.Now:dd/MM/yyyy HH:mm:ss} - {mensagem}";
        File.AppendAllText(_arquivoLog, linhaLog + Environment.NewLine);
        LogRegistrado?.Invoke(mensagem);
    }
}
