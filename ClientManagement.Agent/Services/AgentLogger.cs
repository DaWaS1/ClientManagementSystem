namespace ClientManagement.Agent.Services;

public static class AgentLogger
{
    private static readonly object SyncLock = new();

    private static readonly string LogDirectory =
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.CommonApplicationData),
            "ClientManagement",
            "Agent",
            "Logs");

    private static readonly string LogFilePath =
        Path.Combine(
            LogDirectory,
            "agent.log");


    public static void Write(
        string message)
    {
        string logLine =
            $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";

        // Debugoláskor továbbra is lássuk a konzolon.
        Console.WriteLine(logLine);

        try
        {
            Directory.CreateDirectory(
                LogDirectory);

            lock (SyncLock)
            {
                File.AppendAllText(
                    LogFilePath,
                    logLine +
                    Environment.NewLine);
            }
        }
        catch
        {
            // A logolási hiba önmagában ne állítsa le
            // az Agent működését.
        }
    }
}