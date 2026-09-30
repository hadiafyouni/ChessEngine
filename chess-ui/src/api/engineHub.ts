import * as signalR from '@microsoft/signalr';
import type { AnalysisLine } from '../types';

export class EngineHub {
  private connection: signalR.HubConnection;

  constructor(
    onProgress: (info: AnalysisLine) => void,
    onDone: () => void,
  ) {
    this.connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/engine')
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    this.connection.on('searchProgress', (info: AnalysisLine) => {
      onProgress({ depth: info.depth, score: info.score, nodes: info.nodes, timeMs: info.timeMs, pv: info.pv ?? '' });
    });

    this.connection.on('bestMove', () => onDone());
    this.connection.on('searchError', (msg: string) => { console.error('Hub error:', msg); onDone(); });
  }

  async start(): Promise<void> {
    await this.connection.start();
  }

  async stop(): Promise<void> {
    await this.connection.stop();
  }

  async analyze(fen: string, depth = 14, timeMs = 6000): Promise<void> {
    if (this.connection.state !== signalR.HubConnectionState.Connected) return;
    await this.connection.invoke('FindBestMove', fen, depth, timeMs);
  }
}
