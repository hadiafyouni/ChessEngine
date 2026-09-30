import type { AnalysisLine } from '../types';

interface Props {
  lines: AnalysisLine[];
  isAnalyzing: boolean;
}

function formatScore(score: number): string {
  if (score >= 28000) return `M${Math.ceil((29000 - score) / 2)}`;
  if (score <= -28000) return `-M${Math.ceil((29000 + score) / 2)}`;
  const cp = (score / 100).toFixed(2);
  return score > 0 ? `+${cp}` : cp;
}

function formatNodes(n: number): string {
  if (n >= 1_000_000) return `${(n / 1_000_000).toFixed(1)}M`;
  if (n >= 1_000) return `${Math.round(n / 1_000)}k`;
  return `${n}`;
}

export function AnalysisPanel({ lines, isAnalyzing }: Props) {
  const latest = lines[lines.length - 1];

  return (
    <div className="panel">
      <div className="panel-title">
        Engine Analysis
        {isAnalyzing && <span className="blink-dot" />}
      </div>

      {latest ? (
        <>
          <div className="stats-grid">
            <Stat label="Depth" value={String(latest.depth)} />
            <Stat
              label="Score"
              value={formatScore(latest.score)}
              color={latest.score > 50 ? 'var(--teal)' : latest.score < -50 ? 'var(--red)' : 'var(--fg)'}
            />
            <Stat label="Nodes" value={formatNodes(latest.nodes)} />
            <Stat label="Time" value={`${latest.timeMs}ms`} />
          </div>
          {latest.pv && (
            <div className="pv-box">
              <span className="pv-label">PV </span>
              <span className="pv-moves">{latest.pv}</span>
            </div>
          )}
        </>
      ) : (
        <div className="panel-empty">
          {isAnalyzing ? 'Searching…' : 'Start a game to see analysis'}
        </div>
      )}
    </div>
  );
}

function Stat({ label, value, color }: { label: string; value: string; color?: string }) {
  return (
    <div className="stat">
      <div className="stat-label">{label}</div>
      <div className="stat-value" style={color ? { color } : undefined}>{value}</div>
    </div>
  );
}
