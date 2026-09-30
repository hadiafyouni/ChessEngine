interface Props {
  whiteScore: number; // centipawns from white's perspective; positive = white winning
  height: number;
}

function toWhitePct(score: number): number {
  if (score >= 28000) return 100;
  if (score <= -28000) return 0;
  // Logistic curve: 50% at score=0, approaches 100/0 as score→±∞
  return 100 / (1 + Math.exp(-score / 400));
}

function formatLabel(score: number): string {
  if (score >= 28000) return `M${Math.ceil((29000 - score) / 2)}`;
  if (score <= -28000) return `M${Math.ceil((29000 + score) / 2)}`;
  const abs = Math.abs(score / 100).toFixed(1);
  return abs === '0.0' ? '0.0' : abs;
}

export function EvalBar({ whiteScore, height }: Props) {
  const whitePct = toWhitePct(whiteScore);
  const label = formatLabel(whiteScore);
  const whiteWinning = whiteScore >= 0;

  return (
    <div className="eval-bar" style={{ height }}>
      {/* Black section (top) */}
      <div className="eval-bar-black" style={{ height: `${100 - whitePct}%` }}>
        {!whiteWinning && <span className="eval-label eval-label-black">{label}</span>}
      </div>
      {/* White section (bottom) */}
      <div className="eval-bar-white" style={{ height: `${whitePct}%` }}>
        {whiteWinning && <span className="eval-label eval-label-white">{label}</span>}
      </div>
    </div>
  );
}
