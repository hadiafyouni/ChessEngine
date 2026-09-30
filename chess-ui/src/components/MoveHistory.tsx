import { useEffect, useRef } from 'react';

interface Props {
  moves: string[];
}

export function MoveHistory({ moves }: Props) {
  const bottomRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [moves.length]);

  const pairs: [string, string | undefined][] = [];
  for (let i = 0; i < moves.length; i += 2) {
    pairs.push([moves[i], moves[i + 1]]);
  }

  return (
    <div className="panel moves-panel">
      <div className="panel-title">Move History</div>
      {pairs.length === 0 ? (
        <div className="panel-empty">No moves yet</div>
      ) : (
        <div className="moves-list">
          {pairs.map(([w, b], i) => (
            <div key={i} className="move-row">
              <span className="move-num">{i + 1}.</span>
              <span className="move-cell">{w}</span>
              {b && <span className="move-cell">{b}</span>}
            </div>
          ))}
          <div ref={bottomRef} />
        </div>
      )}
    </div>
  );
}
