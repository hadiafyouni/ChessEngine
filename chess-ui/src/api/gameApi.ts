export interface StartGameResponse {
  gameId: string;
  fen: string;
  status: string;
}

export interface MakeMoveResponse {
  gameId: string;
  playerMove: string;
  aiMove: string | null;
  fen: string;
  status: string;
}

export async function startGame(playerColor: string): Promise<StartGameResponse> {
  const res = await fetch('/api/game/start', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ playerColor }),
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({ error: res.statusText }));
    throw new Error(err.error ?? res.statusText);
  }
  return res.json();
}

export async function makeMove(gameId: string, move: string): Promise<MakeMoveResponse> {
  const res = await fetch('/api/game/move', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ gameId, move }),
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({ error: res.statusText }));
    throw new Error(err.error ?? res.statusText);
  }
  return res.json();
}

export async function resignGame(gameId: string): Promise<void> {
  await fetch(`/api/game/${gameId}/resign`, { method: 'POST' });
}
