using Chess.API.Models;
using Chess.Engine.AI;
using Chess.Engine.Core;
using Microsoft.AspNetCore.SignalR;

namespace Chess.API.Hubs;

/// <summary>
/// SignalR hub for real-time engine analysis with streaming search progress.
/// Connect at: /hubs/engine
/// </summary>
public class EngineHub : Hub
{
    private readonly IServiceProvider _services;
    private readonly ILogger<EngineHub> _logger;

    public EngineHub(IServiceProvider services, ILogger<EngineHub> logger)
    {
        _services = services;
        _logger = logger;
    }

    /// <summary>
    /// Start a search and stream progress updates back to the caller.
    /// Client receives:
    ///   - "searchProgress" messages with { depth, score, nodes, timeMs, pv }
    ///   - "bestMove" message with the final result
    ///   - "searchError" message on failure
    /// </summary>
    public async Task FindBestMove(string fen, int depth = 10, int timeMs = 5000)
    {
        if (string.IsNullOrWhiteSpace(fen))
        {
            await Clients.Caller.SendAsync("searchError", "FEN string is required.");
            return;
        }

        try
        {
            var board = Board.FromFEN(fen);
            var search = _services.GetRequiredService<Search>();

            depth = Math.Clamp(depth, 1, 30);
            timeMs = Math.Clamp(timeMs, 100, 60_000);

            var connectionId = Context.ConnectionId;

            // Subscribe to search progress events and forward them to the caller
            search.OnSearchProgress += async (info) =>
            {
                try
                {
                    await Clients.Client(connectionId).SendAsync("searchProgress",
                        new SearchProgressUpdate(
                            info.Depth,
                            info.Score,
                            info.Nodes,
                            info.TimeMs,
                            info.Pv
                        ));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send search progress to {ConnectionId}", connectionId);
                }
            };

            // Run the search on a thread pool thread to avoid blocking the hub
            var result = await Task.Run(() => search.FindBestMove(board, depth, timeMs));

            string bestMoveStr = result.BestMove == Move.None ? "none" : result.BestMove.ToString();

            await Clients.Caller.SendAsync("bestMove",
                new BestMoveResult(
                    bestMoveStr,
                    result.Score,
                    result.Depth,
                    result.Nodes,
                    result.TimeMs
                ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in FindBestMove hub method for FEN: {Fen}", fen);
            await Clients.Caller.SendAsync("searchError", $"Engine error: {ex.Message}");
        }
    }

    public override Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
