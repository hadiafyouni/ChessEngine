namespace Chess.API.Models;

// ─── Engine DTOs ───────────────────────────────────────────

public record FindMoveRequest(string Fen, int Depth = 10, int TimeMs = 5000);

public record FindMoveResponse(
    string BestMove,
    int Score,
    int Depth,
    long Nodes,
    long TimeMs
);

public record EvaluateRequest(string Fen);

public record EvaluateResponse(string Fen, int Score);

public record PerftResponse(string Fen, int Depth, long Nodes, long TimeMs);

// ─── Game DTOs ─────────────────────────────────────────────

public record StartGameRequest(string? PlayerColor = "white");

public record StartGameResponse(string GameId, string Fen, string Status);

public record MakeMoveRequest(string GameId, string Move);

public record MakeMoveResponse(
    string GameId,
    string PlayerMove,
    string? AiMove,
    string Fen,
    string Status
);

public record GameStateResponse(
    string GameId,
    string Fen,
    List<string> Moves,
    string Status
);

public record ResignResponse(string GameId, string Status);

// ─── SignalR DTOs ──────────────────────────────────────────

public record SearchProgressUpdate(
    int Depth,
    int Score,
    long Nodes,
    long TimeMs,
    string? Pv
);

public record BestMoveResult(
    string BestMove,
    int Score,
    int Depth,
    long Nodes,
    long TimeMs
);

// ─── Internal Game State ───────────────────────────────────

public class GameState
{
    public string GameId { get; set; } = string.Empty;
    public string PlayerColor { get; set; } = "white";
    public string Fen { get; set; } = string.Empty;
    public List<string> Moves { get; set; } = new();
    public string Status { get; set; } = "InProgress"; // InProgress, Checkmate, Stalemate, Draw, Resigned
}

public record ErrorResponse(string Error);
