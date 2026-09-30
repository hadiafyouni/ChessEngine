using System.Collections.Concurrent;
using Chess.API.Models;
using Chess.Engine.AI;
using Chess.Engine.Core;
using Microsoft.AspNetCore.Mvc;

namespace Chess.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GameController : ControllerBase
{
    // In-memory game storage shared across requests
    private static readonly ConcurrentDictionary<string, GameState> Games = new();

    private readonly IServiceProvider _services;
    private readonly ILogger<GameController> _logger;

    public GameController(IServiceProvider services, ILogger<GameController> logger)
    {
        _services = services;
        _logger = logger;
    }

    /// <summary>
    /// Start a new game against the engine.
    /// </summary>
    [HttpPost("start")]
    [ProducesResponseType(typeof(StartGameResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public IActionResult StartGame([FromBody] StartGameRequest? request)
    {
        try
        {
            string playerColor = (request?.PlayerColor ?? "white").ToLowerInvariant();
            if (playerColor != "white" && playerColor != "black")
                return BadRequest(new ErrorResponse("PlayerColor must be 'white' or 'black'."));

            string gameId = Guid.NewGuid().ToString("N")[..12];
            string startFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

            var gameState = new GameState
            {
                GameId = gameId,
                PlayerColor = playerColor,
                Fen = startFen,
                Status = "InProgress"
            };

            Games[gameId] = gameState;

            // If player is black, AI plays first as white
            if (playerColor == "black")
            {
                var board = Board.FromFEN(startFen);
                var aiMove = GetAiMove(board);
                if (aiMove != Move.None)
                {
                    board.MakeMove(aiMove);
                    string aiMoveStr = aiMove.ToString();
                    gameState.Moves.Add(aiMoveStr);
                    gameState.Fen = board.ToFEN();
                    UpdateGameStatus(board, gameState);
                }
            }

            return Ok(new StartGameResponse(gameState.GameId, gameState.Fen, gameState.Status));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting game");
            return BadRequest(new ErrorResponse($"Failed to start game: {ex.Message}"));
        }
    }

    /// <summary>
    /// Make a move in an active game. The engine will respond automatically.
    /// </summary>
    [HttpPost("move")]
    [ProducesResponseType(typeof(MakeMoveResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public IActionResult MakeMove([FromBody] MakeMoveRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.GameId))
            return BadRequest(new ErrorResponse("GameId is required."));
        if (string.IsNullOrWhiteSpace(request.Move))
            return BadRequest(new ErrorResponse("Move is required."));

        if (!Games.TryGetValue(request.GameId, out var gameState))
            return NotFound(new ErrorResponse($"Game '{request.GameId}' not found."));

        if (gameState.Status != "InProgress")
            return BadRequest(new ErrorResponse($"Game is already over: {gameState.Status}"));

        try
        {
            var board = Board.FromFEN(gameState.Fen);

            // Validate it's the player's turn
            bool whiteToMove = board.WhiteToMove;
            bool isPlayerTurn = (gameState.PlayerColor == "white" && whiteToMove)
                             || (gameState.PlayerColor == "black" && !whiteToMove);
            if (!isPlayerTurn)
                return BadRequest(new ErrorResponse("It is not your turn."));

            // Parse and validate the player's move
            var playerMove = ParseAndValidateMove(board, request.Move);
            if (playerMove == Move.None)
                return BadRequest(new ErrorResponse($"Illegal move: '{request.Move}'."));

            // Apply player move
            board.MakeMove(playerMove);
            string playerMoveStr = playerMove.ToString();
            gameState.Moves.Add(playerMoveStr);
            gameState.Fen = board.ToFEN();

            // Check game state after player move
            UpdateGameStatus(board, gameState);
            string? aiMoveStr = null;

            // If game still in progress, have AI respond
            if (gameState.Status == "InProgress")
            {
                var aiMove = GetAiMove(board);
                if (aiMove != Move.None)
                {
                    board.MakeMove(aiMove);
                    aiMoveStr = aiMove.ToString();
                    gameState.Moves.Add(aiMoveStr);
                    gameState.Fen = board.ToFEN();
                    UpdateGameStatus(board, gameState);
                }
                else
                {
                    // AI has no legal moves – already handled by UpdateGameStatus
                    UpdateGameStatus(board, gameState);
                }
            }

            return Ok(new MakeMoveResponse(
                gameState.GameId,
                playerMoveStr,
                aiMoveStr,
                gameState.Fen,
                gameState.Status
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error making move in game {GameId}", request.GameId);
            return BadRequest(new ErrorResponse($"Error processing move: {ex.Message}"));
        }
    }

    /// <summary>
    /// Get the current state of a game.
    /// </summary>
    [HttpGet("{id}/state")]
    [ProducesResponseType(typeof(GameStateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public IActionResult GetGameState(string id)
    {
        if (!Games.TryGetValue(id, out var gameState))
            return NotFound(new ErrorResponse($"Game '{id}' not found."));

        return Ok(new GameStateResponse(
            gameState.GameId,
            gameState.Fen,
            gameState.Moves.ToList(),
            gameState.Status
        ));
    }

    /// <summary>
    /// Resign from an active game.
    /// </summary>
    [HttpPost("{id}/resign")]
    [ProducesResponseType(typeof(ResignResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public IActionResult Resign(string id)
    {
        if (!Games.TryGetValue(id, out var gameState))
            return NotFound(new ErrorResponse($"Game '{id}' not found."));

        if (gameState.Status != "InProgress")
            return BadRequest(new ErrorResponse($"Game is already over: {gameState.Status}"));

        gameState.Status = "Resigned";
        return Ok(new ResignResponse(gameState.GameId, gameState.Status));
    }

    // ─── Helpers ───────────────────────────────────────────

    /// <summary>
    /// Parse a UCI move string (e.g. "e2e4", "e7e8q") and find the matching legal move.
    /// </summary>
    private static Move ParseAndValidateMove(Board board, string uciMove)
    {
        uciMove = uciMove.Trim().ToLowerInvariant();
        if (uciMove.Length < 4 || uciMove.Length > 5)
            return Move.None;

        int fromFile = uciMove[0] - 'a';
        int fromRank = uciMove[1] - '1';
        int toFile = uciMove[2] - 'a';
        int toRank = uciMove[3] - '1';

        if (fromFile < 0 || fromFile > 7 || fromRank < 0 || fromRank > 7 ||
            toFile < 0 || toFile > 7 || toRank < 0 || toRank > 7)
            return Move.None;

        int from = fromRank * 8 + fromFile;
        int to = toRank * 8 + toFile;

        char? promoChar = uciMove.Length == 5 ? uciMove[4] : null;

        Span<Move> legalMoves = MoveGenerator.GenerateMoves(board);

        for (int i = 0; i < legalMoves.Length; i++)
        {
            var m = legalMoves[i];
            if (m.From != from || m.To != to)
                continue;

            // If promotion, match the promotion piece
            if (m.IsPromotion)
            {
                if (promoChar == null)
                {
                    // Default to queen promotion if not specified
                    if (m.Flag == MoveFlag.PromoteToQueen || m.Flag == MoveFlag.PromoteToQueenCapture)
                        return m;
                    continue;
                }

                char promLetter = GetPromotionChar(m.Promotion);
                if (promLetter == promoChar.Value)
                    return m;
            }
            else
            {
                // Non-promotion move, just match from/to
                return m;
            }
        }

        return Move.None;
    }

    private static char GetPromotionChar(PieceType promotion) => promotion switch
    {
        PieceType.WhiteKnight or PieceType.BlackKnight => 'n',
        PieceType.WhiteBishop or PieceType.BlackBishop => 'b',
        PieceType.WhiteRook or PieceType.BlackRook => 'r',
        PieceType.WhiteQueen or PieceType.BlackQueen => 'q',
        _ => '?'
    };

    /// <summary>
    /// Use the Search engine to find the best move for the current position.
    /// </summary>
    private Move GetAiMove(Board board)
    {
        var search = _services.GetRequiredService<Search>();
        var result = search.FindBestMove(board, maxDepth: 8, maxTimeMs: 3000);
        return result.BestMove;
    }

    /// <summary>
    /// Update the game status based on the current board position.
    /// </summary>
    private static void UpdateGameStatus(Board board, GameState gameState)
    {
        Span<Move> legalMoves = MoveGenerator.GenerateMoves(board);

        if (legalMoves.Length == 0)
        {
            if (board.IsInCheck(board.WhiteToMove))
            {
                gameState.Status = "Checkmate";
            }
            else
            {
                gameState.Status = "Stalemate";
            }
            return;
        }

        // 50-move rule
        if (board.HalfMoveClock >= 100)
        {
            gameState.Status = "Draw";
            return;
        }

        // Insufficient material check (K vs K)
        bool onlyKings = true;
        for (int i = 0; i < 12; i++)
        {
            if (i == (int)PieceType.WhiteKing || i == (int)PieceType.BlackKing)
                continue;
            if (board.Pieces[i] != 0)
            {
                onlyKings = false;
                break;
            }
        }
        if (onlyKings)
        {
            gameState.Status = "Draw";
        }
    }
}
