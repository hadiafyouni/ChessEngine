using System.Diagnostics;
using Chess.API.Models;
using Chess.Engine.AI;
using Chess.Engine.Core;
using Microsoft.AspNetCore.Mvc;

namespace Chess.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EngineController : ControllerBase
{
    private readonly IServiceProvider _services;
    private readonly ILogger<EngineController> _logger;

    public EngineController(IServiceProvider services, ILogger<EngineController> logger)
    {
        _services = services;
        _logger = logger;
    }

    /// <summary>
    /// Find the best move for a given position.
    /// </summary>
    [HttpPost("move")]
    [ProducesResponseType(typeof(FindMoveResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public IActionResult FindBestMove([FromBody] FindMoveRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Fen))
            return BadRequest(new ErrorResponse("FEN string is required."));

        try
        {
            var board = Board.FromFEN(request.Fen);
            var search = _services.GetRequiredService<Search>();

            int depth = Math.Clamp(request.Depth, 1, 30);
            int timeMs = Math.Clamp(request.TimeMs, 100, 60_000);

            var result = search.FindBestMove(board, depth, timeMs);

            if (result.BestMove == Move.None)
            {
                // No legal move found – could be checkmate or stalemate
                return Ok(new FindMoveResponse("none", result.Score, result.Depth, result.Nodes, result.TimeMs));
            }

            return Ok(new FindMoveResponse(
                result.BestMove.ToString(),
                result.Score,
                result.Depth,
                result.Nodes,
                result.TimeMs
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding best move for FEN: {Fen}", request.Fen);
            return BadRequest(new ErrorResponse($"Invalid FEN or engine error: {ex.Message}"));
        }
    }

    /// <summary>
    /// Statically evaluate a position (no search, just evaluation function).
    /// </summary>
    [HttpPost("evaluate")]
    [ProducesResponseType(typeof(EvaluateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public IActionResult Evaluate([FromBody] EvaluateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Fen))
            return BadRequest(new ErrorResponse("FEN string is required."));

        try
        {
            var board = Board.FromFEN(request.Fen);
            int score = Evaluation.Evaluate(board);
            return Ok(new EvaluateResponse(request.Fen, score));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating FEN: {Fen}", request.Fen);
            return BadRequest(new ErrorResponse($"Invalid FEN: {ex.Message}"));
        }
    }

    /// <summary>
    /// Run a Perft test (count leaf nodes at a given depth).
    /// </summary>
    [HttpGet("perft/{depth:int}")]
    [ProducesResponseType(typeof(PerftResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public IActionResult Perft(int depth, [FromQuery] string? fen = null)
    {
        fen ??= "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

        if (depth < 0 || depth > 8)
            return BadRequest(new ErrorResponse("Depth must be between 0 and 8."));

        try
        {
            var board = Board.FromFEN(fen);
            var sw = Stopwatch.StartNew();
            long nodes = PerftCount(board, depth);
            sw.Stop();

            return Ok(new PerftResponse(fen, depth, nodes, sw.ElapsedMilliseconds));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running perft for FEN: {Fen}, depth: {Depth}", fen, depth);
            return BadRequest(new ErrorResponse($"Invalid FEN or perft error: {ex.Message}"));
        }
    }

    /// <summary>
    /// Recursive Perft node counter using the engine's MoveGenerator.
    /// </summary>
    private static long PerftCount(Board board, int depth)
    {
        if (depth == 0) return 1;

        Span<Move> moves = stackalloc Move[256];
        int count = MoveGenerator.GenerateMoves(board, moves, false);
        long nodes = 0;

        for (int i = 0; i < count; i++)
        {
            var state = board.MakeMove(moves[i]);
            nodes += PerftCount(board, depth - 1);
            board.UnmakeMove(moves[i], state);
        }

        return nodes;
    }
}
