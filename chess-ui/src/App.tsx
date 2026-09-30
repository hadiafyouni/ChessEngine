import { useState, useEffect, useRef, useCallback } from 'react';
import { Chessboard } from 'react-chessboard';
import { Chess } from 'chess.js';
import { AnalysisPanel } from './components/AnalysisPanel';
import { EvalBar } from './components/EvalBar';
import { MoveHistory } from './components/MoveHistory';
import { EngineHub } from './api/engineHub';
import { startGame, makeMove, resignGame } from './api/gameApi';
import type { PieceDropHandlerArgs } from 'react-chessboard';
import type { PlayerColor, GameStatus, AnalysisLine } from './types';
import './App.css';

const STARTING_FEN = 'rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1';
const BOARD_SIZE = 580;

export default function App() {
  const [screen, setScreen] = useState<'setup' | 'game'>('setup');
  const [playerColor, setPlayerColor] = useState<PlayerColor>('white');

  const [gameId, setGameId] = useState<string | null>(null);
  const [fen, setFen] = useState(STARTING_FEN);
  const [status, setStatus] = useState<GameStatus>('Idle');
  const [moves, setMoves] = useState<string[]>([]);
  const [thinking, setThinking] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [analysisLines, setAnalysisLines] = useState<AnalysisLine[]>([]);
  const [isAnalyzing, setIsAnalyzing] = useState(false);
  const [analysisForWhite, setAnalysisForWhite] = useState(true);

  const hubRef = useRef<EngineHub | null>(null);

  useEffect(() => {
    const hub = new EngineHub(
      (info) => setAnalysisLines(prev => [...prev, info]),
      () => setIsAnalyzing(false),
    );
    hub.start().catch(console.error);
    hubRef.current = hub;
    return () => { hub.stop(); };
  }, []);

  const analyzePosition = useCallback((positionFen: string) => {
    const c = new Chess(positionFen);
    setAnalysisForWhite(c.turn() === 'w');
    setAnalysisLines([]);
    setIsAnalyzing(true);
    hubRef.current?.analyze(positionFen, 14, 6000).catch(console.error);
  }, []);

  const handleStart = async () => {
    setError(null);
    setThinking(true);
    try {
      const res = await startGame(playerColor);
      setGameId(res.gameId);
      setFen(res.fen);
      setStatus(res.status as GameStatus);
      setMoves([]);
      setScreen('game');
      analyzePosition(res.fen);
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setThinking(false);
    }
  };

  const handleDrop = useCallback(({ piece, sourceSquare, targetSquare }: PieceDropHandlerArgs): boolean => {
    if (!targetSquare || !gameId || status !== 'InProgress' || thinking) return false;

    const c = new Chess(fen);
    if ((c.turn() === 'w') !== (playerColor === 'white')) return false;

    let promotion = '';
    if (piece.pieceType[1] === 'P') {
      const toRank = parseInt(targetSquare[1]);
      if ((playerColor === 'white' && toRank === 8) || (playerColor === 'black' && toRank === 1))
        promotion = 'q';
    }

    setThinking(true);
    setError(null);

    makeMove(gameId, sourceSquare + targetSquare + promotion)
      .then(res => {
        setMoves(prev => {
          const next = [...prev, res.playerMove];
          if (res.aiMove) next.push(res.aiMove);
          return next;
        });
        setFen(res.fen);
        setStatus(res.status as GameStatus);
        analyzePosition(res.fen);
      })
      .catch(e => setError(e instanceof Error ? e.message : String(e)))
      .finally(() => setThinking(false));

    return true;
  }, [gameId, fen, status, thinking, playerColor, analyzePosition]);

  const handleResign = async () => {
    if (!gameId) return;
    await resignGame(gameId);
    setStatus('Resigned');
  };

  const isGameOver = status !== 'InProgress' && status !== 'Idle';
  const boardChess = new Chess(fen);
  const isMyTurn = screen === 'game' && (boardChess.turn() === 'w') === (playerColor === 'white');
  const latestScore = analysisLines.length > 0 ? analysisLines[analysisLines.length - 1].score : 0;
  const whiteScore = analysisForWhite ? latestScore : -latestScore;

  // ── Setup screen ───────────────────────────────────────────
  if (screen === 'setup') {
    return (
      <div className="app center">
        <div className="setup-card">
          <h1 className="logo">♟ Chess Engine</h1>
          <p className="subtitle">Play against a custom alpha-beta engine</p>

          <div className="color-picker">
            {(['white', 'black'] as PlayerColor[]).map(c => (
              <button
                key={c}
                className={`color-btn${playerColor === c ? ' active' : ''}`}
                onClick={() => setPlayerColor(c)}
              >
                {c === 'white' ? '♙ White' : '♟ Black'}
              </button>
            ))}
          </div>

          {error && <div className="error-box">{error}</div>}

          <button className="btn-primary" onClick={handleStart} disabled={thinking}>
            {thinking ? 'Starting…' : 'Start Game'}
          </button>
        </div>
      </div>
    );
  }

  // ── Game screen ────────────────────────────────────────────
  return (
    <div className="app center">
      <div className="game-layout">

        <div className="board-col">
          <div className="board-with-bar">
            <EvalBar whiteScore={whiteScore} height={BOARD_SIZE} />
            <div className="board-frame">
              <div style={{ width: BOARD_SIZE }}>
                <Chessboard options={{
                  position: fen,
                  onPieceDrop: handleDrop,
                  boardOrientation: playerColor,
                  allowDragging: !thinking && !isGameOver && isMyTurn,
                  boardStyle: { borderRadius: 2 },
                  darkSquareStyle: { backgroundColor: '#b58863' },
                  lightSquareStyle: { backgroundColor: '#f0d9b5' },
                }} />
              </div>
            </div>
          </div>

          <div className="board-footer">
            <span className={`status-badge status-${status.toLowerCase()}`}>
              {statusLabel(status, isMyTurn, thinking)}
            </span>
            <div className="footer-actions">
              {!isGameOver && (
                <button className="btn-ghost" onClick={handleResign} disabled={thinking}>
                  Resign
                </button>
              )}
              {isGameOver && (
                <button className="btn-primary small" onClick={() => setScreen('setup')}>
                  New Game
                </button>
              )}
            </div>
          </div>

          {error && <div className="error-box">{error}</div>}
        </div>

        <div className="sidebar">
          <AnalysisPanel lines={analysisLines} isAnalyzing={isAnalyzing} />
          <MoveHistory moves={moves} />
        </div>

      </div>
    </div>
  );
}

function statusLabel(status: GameStatus, isMyTurn: boolean, thinking: boolean): string {
  if (thinking) return '⏳ Engine thinking…';
  switch (status) {
    case 'Checkmate':  return '♟ Checkmate';
    case 'Stalemate':  return '½ Stalemate';
    case 'Draw':       return '½ Draw';
    case 'Resigned':   return '🏳 Resigned';
    case 'InProgress': return isMyTurn ? '▶ Your turn' : '⏳ Waiting…';
    default: return '';
  }
}
