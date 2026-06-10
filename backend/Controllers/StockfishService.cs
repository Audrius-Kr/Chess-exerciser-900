using CHESSPROJ.Controllers;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace CHESSPROJ.Services
{
    public class StockfishService : IStockfishService, IDisposable
    {
        private readonly Process _process;
        private readonly object _lock = new();

        public StockfishService(string stockfishPath)
        {
            _process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = stockfishPath,
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };
            _process.Start();
            SendCommand("uci");
            WaitFor("uciok");
            SendCommand("isready");
            WaitFor("readyok");
        }

        public void SetLevel(int level)
        {
            // Stockfish Skill Level: 0 (weakest) to 20 (strongest)
            SendCommand($"setoption name Skill Level value {Math.Clamp(level * 4, 0, 20)}");
        }

        public void SetPosition(string movesMade, string move)
        {
            SendCommand($"position startpos moves {movesMade} {move}");
        }

        public string GetBestMove()
        {
            SendCommand("go depth 5");
            var output = WaitFor("bestmove");
            // Parse "bestmove e2e4" from output
            var match = Regex.Match(output, @"bestmove\s+(\S+)");
            return match.Success ? match.Groups[1].Value : "";
        }

        public bool IsMoveCorrect(string currentPosition, string move)
        {
            SendCommand($"position startpos moves {currentPosition}");
            SendCommand("go perft 1");
            var output = WaitFor("Nodes searched");
            // perft lists legal moves; if the move is legal it's in the list
            SendCommand($"position startpos moves {currentPosition} {move}");
            SendCommand("d");
            var board = WaitFor("Checkers");
            // If the move was applied (board changed), it's correct
            return !board.Contains("Unknown command") && !board.Contains("No such");
        }

        public string GetFen()
        {
            SendCommand("d");
            var output = WaitFor("Checkers");
            var match = Regex.Match(output, @"Fen:\s*(.+)\s*$", RegexOptions.Multiline);
            if (match.Success)
                return match.Groups[1].Value.Trim();

            match = Regex.Match(output, @"Fen:\s*(.+)", RegexOptions.Multiline);
            return match.Success ? match.Groups[1].Value.Trim() : "";
        }

        public string GetEvalType()
        {
            SendCommand("eval");
            var output = WaitFor("Final evaluation");
            if (output.Contains("mate"))
                return "mate";
            return "cp";
        }

        public int GetEvalVal()
        {
            SendCommand("eval");
            var output = WaitFor("Final evaluation");
            var match = Regex.Match(output, @"Final evaluation:\s+(-?\d+)");
            if (match.Success)
                return int.Parse(match.Groups[1].Value);
            return 0;
        }

        private void SendCommand(string command)
        {
            lock (_lock)
            {
                _process.StandardInput.WriteLine(command);
                _process.StandardInput.Flush();
            }
        }

        private string WaitFor(string marker)
        {
            lock (_lock)
            {
                var output = new System.Text.StringBuilder();
                string? line;
                while ((line = _process.StandardOutput.ReadLine()) != null)
                {
                    output.AppendLine(line);
                    if (line.Contains(marker))
                        return output.ToString();
                }
                return output.ToString();
            }
        }

        public void Dispose()
        {
            SendCommand("quit");
            _process.WaitForExit(2000);
            if (!_process.HasExited)
                _process.Kill();
            _process.Dispose();
        }
    }
}
