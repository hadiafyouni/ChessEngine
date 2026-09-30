export type PlayerColor = 'white' | 'black';
export type GameStatus = 'Idle' | 'InProgress' | 'Checkmate' | 'Stalemate' | 'Draw' | 'Resigned';

export interface AnalysisLine {
  depth: number;
  score: number;
  nodes: number;
  timeMs: number;
  pv: string;
}
