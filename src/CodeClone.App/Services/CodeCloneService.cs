using System.Diagnostics;
using CodeClone.Domain;

namespace CodeClone.App.Services;

/// <summary>
/// Service for invoking the codeclone CLI and parsing results.
/// </summary>
public class CodeCloneService
{
    private readonly TimeSpan _timeout = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Run codeclone analyze on a repository.
    /// </summary>
    public async Task<AnalyzeResult> AnalyzeAsync(string repoPath, CancellationToken ct = default)
    {
        var cliPath = await FindCliAsync(ct);
        if (cliPath is null)
        {
            return AnalyzeResult.Failure("codeclone CLI not found. Ensure it's installed and in PATH.");
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = cliPath,
                Arguments = $"analyze \"{repoPath}\" --format json",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = repoPath
            };

            using var process = new Process { StartInfo = psi };
            process.Start();

            var outputTask = process.StandardOutput.ReadToEndAsync(ct);
            var errorTask = process.StandardError.ReadToEndAsync(ct);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(_timeout);

            await process.WaitForExitAsync(cts.Token);

            var stdout = await outputTask;
            var stderr = await errorTask;

            if (process.ExitCode != 0 && string.IsNullOrWhiteSpace(stdout))
            {
                return AnalyzeResult.Failure($"codeclone exited with code {process.ExitCode}: {stderr}");
            }

            if (AnalyzeResponseParser.TryParse(stdout, out var response, out var parseError))
            {
                return AnalyzeResult.Success(response!);
            }

            return AnalyzeResult.Failure($"Failed to parse codeclone output: {parseError}");
        }
        catch (OperationCanceledException)
        {
            return AnalyzeResult.Failure("Analysis timed out");
        }
        catch (Exception ex)
        {
            return AnalyzeResult.Failure($"Error running codeclone: {ex.Message}");
        }
    }

    /// <summary>
    /// Find the codeclone CLI executable.
    /// </summary>
    private async Task<string?> FindCliAsync(CancellationToken ct)
    {
        // Try direct path first
        var candidates = new[]
        {
            "codeclone",
            "codeclone.exe",
            "python -m codeclone_cli"
        };

        foreach (var candidate in candidates)
        {
            if (await TryExecuteAsync(candidate, "--version", ct))
            {
                return candidate.Contains(' ') ? candidate.Split(' ')[0] : candidate;
            }
        }

        // Check PATH
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        var paths = pathEnv.Split(Path.PathSeparator);

        foreach (var path in paths)
        {
            var exePath = Path.Combine(path, "codeclone.exe");
            if (File.Exists(exePath))
                return exePath;

            var scriptPath = Path.Combine(path, "codeclone");
            if (File.Exists(scriptPath))
                return scriptPath;
        }

        return null;
    }

    private async Task<bool> TryExecuteAsync(string command, string args, CancellationToken ct)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = command.Contains(' ') ? command.Split(' ')[0] : command,
                Arguments = command.Contains(' ') ? $"{string.Join(' ', command.Split(' ').Skip(1))} {args}" : args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = psi };
            process.Start();
            await process.WaitForExitAsync(ct);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// Result of a codeclone analysis operation.
/// </summary>
public record AnalyzeResult
{
    public bool IsSuccess { get; init; }
    public AnalyzeResponse? Response { get; init; }
    public string? Error { get; init; }

    public static AnalyzeResult Success(AnalyzeResponse response) =>
        new() { IsSuccess = true, Response = response };

    public static AnalyzeResult Failure(string error) =>
        new() { IsSuccess = false, Error = error };
}
