const pptx = require("pptxgenjs");

const prs = new pptx();

// ─── Theme ────────────────────────────────────────────────────────────────────
const C = {
  bg:        "0D1117",   // deep dark
  panel:     "161B22",   // card bg
  border:    "30363D",   // subtle border
  accent:    "F78166",   // orange-red (white pieces)
  accent2:   "79C0FF",   // blue (black pieces / engine)
  green:     "56D364",   // positive / pruned
  yellow:    "E3B341",   // warnings / hash
  white:     "E6EDF3",
  dim:       "8B949E",
  pruned:    "3D1F1F",   // pruned branch bg
};

prs.layout = "LAYOUT_WIDE";
prs.theme = { headFontFace: "Segoe UI", bodyFontFace: "Consolas" };

// ─── Helpers ─────────────────────────────────────────────────────────────────
function slide(title, subtitle) {
  const s = prs.addSlide();
  s.background = { color: C.bg };
  if (title) {
    s.addText(title, {
      x: 0.4, y: 0.18, w: 12.5, h: 0.6,
      fontSize: 28, bold: true, color: C.white,
      fontFace: "Segoe UI",
    });
  }
  if (subtitle) {
    s.addText(subtitle, {
      x: 0.4, y: 0.76, w: 12.5, h: 0.35,
      fontSize: 13, color: C.dim, fontFace: "Segoe UI",
    });
  }
  // thin accent line under title
  s.addShape(prs.ShapeType.rect, {
    x: 0.4, y: 1.1, w: 12.5, h: 0.025,
    fill: { color: C.accent }, line: { color: C.accent },
  });
  return s;
}

function panel(s, x, y, w, h, color) {
  s.addShape(prs.ShapeType.roundRect, {
    x, y, w, h,
    fill: { color: color || C.panel },
    line: { color: C.border, width: 0.75 },
    rectRadius: 0.08,
  });
}

function label(s, txt, x, y, w, h, opts = {}) {
  s.addText(txt, {
    x, y, w, h,
    fontSize: opts.size || 12,
    bold: opts.bold || false,
    color: opts.color || C.white,
    align: opts.align || "left",
    fontFace: opts.mono ? "Consolas" : "Segoe UI",
    valign: "middle",
    wrap: true,
  });
}

function codeBox(s, code, x, y, w, h) {
  panel(s, x, y, w, h, "0D1117");
  s.addShape(prs.ShapeType.rect, {
    x, y, w: 0.06, h,
    fill: { color: C.accent2 }, line: { color: C.accent2 },
  });
  s.addText(code, {
    x: x + 0.12, y, w: w - 0.14, h,
    fontSize: 10.5, color: C.green, fontFace: "Consolas",
    valign: "middle", wrap: true,
  });
}

// ─────────────────────────────────────────────────────────────────────────────
// SLIDE 1 – Title
// ─────────────────────────────────────────────────────────────────────────────
{
  const s = prs.addSlide();
  s.background = { color: C.bg };

  // big chess board art (Unicode)
  s.addText("♟", { x: 9.5, y: 0.5, w: 3.5, h: 5, fontSize: 220, color: "1C2128", align: "center" });

  s.addText("Chess Engine", { x: 0.6, y: 1.2, w: 9, h: 1, fontSize: 52, bold: true, color: C.white, fontFace: "Segoe UI" });
  s.addText("Architecture", { x: 0.6, y: 2.1, w: 9, h: 1, fontSize: 52, bold: true, color: C.accent, fontFace: "Segoe UI" });
  s.addText("From mouse-click to engine reply — every algorithm explained", {
    x: 0.6, y: 3.2, w: 8.5, h: 0.5, fontSize: 16, color: C.dim, fontFace: "Segoe UI",
  });

  s.addShape(prs.ShapeType.rect, { x: 0.6, y: 4.1, w: 2, h: 0.06, fill: { color: C.accent }, line: { color: C.accent } });

  const topics = ["Bitboard Representation", "Zobrist Hashing", "Move Generation", "Iterative Deepening",
                  "Alpha-Beta Pruning", "Move Ordering", "Quiescence Search", "Static Evaluation"];
  topics.forEach((t, i) => {
    const col = i < 4 ? 0 : 1;
    const row = i % 4;
    s.addText(`${i + 1}. ${t}`, { x: 0.6 + col * 4, y: 4.4 + row * 0.35, w: 3.8, h: 0.32, fontSize: 12, color: C.dim });
  });
}

// ─────────────────────────────────────────────────────────────────────────────
// SLIDE 2 – What IS a Bitboard?
// ─────────────────────────────────────────────────────────────────────────────
{
  const s = slide("What Is a Bitboard?", "Each piece type is stored as one 64-bit integer — one bit per square");

  label(s, "A chess board has exactly 64 squares.  A 64-bit integer has exactly 64 bits.\nThe engine maps them 1-to-1.", 0.4, 1.2, 8, 0.7, { color: C.dim, size: 13 });

  // Draw 8×8 grid
  const gx = 0.4, gy = 1.95, sq = 0.72;
  const whitePawns = [8,9,10,11,12,13,14,15]; // rank 2

  for (let r = 0; r < 8; r++) {
    for (let c = 0; c < 8; c++) {
      const idx = r * 8 + c;
      const isPawn = whitePawns.includes(idx);
      const isLight = (r + c) % 2 === 0;
      const fillColor = isPawn ? C.accent : (isLight ? "21262D" : "161B22");
      s.addShape(prs.ShapeType.rect, {
        x: gx + c * sq, y: gy + r * sq, w: sq, h: sq,
        fill: { color: fillColor },
        line: { color: C.border, width: 0.5 },
      });
      // rank 2 pawn symbol
      if (isPawn) {
        s.addText("♙", { x: gx + c * sq, y: gy + r * sq, w: sq, h: sq, fontSize: 22, align: "center", valign: "middle", color: C.bg });
      }
      // bit index (small)
      s.addText(String(idx), {
        x: gx + c * sq + 0.04, y: gy + r * sq + 0.04, w: sq - 0.06, h: 0.22,
        fontSize: 6.5, color: isPawn ? C.bg : C.border, align: "left",
      });
    }
  }

  // File labels
  "abcdefgh".split("").forEach((f, i) => {
    s.addText(f, { x: gx + i * sq, y: gy + 8 * sq + 0.04, w: sq, h: 0.25, fontSize: 10, color: C.dim, align: "center" });
  });
  for (let r = 0; r < 8; r++) {
    s.addText(String(8 - r), { x: gx - 0.25, y: gy + r * sq, w: 0.22, h: sq, fontSize: 10, color: C.dim, align: "right", valign: "middle" });
  }

  // Right panel
  panel(s, 6.4, 1.95, 6.8, 5.6);

  label(s, "White Pawns Bitboard", 6.7, 2.1, 5, 0.3, { bold: true, size: 14, color: C.accent });
  label(s, "Starting position — pawns on rank 2 (bits 8–15)", 6.7, 2.38, 5.8, 0.28, { color: C.dim, size: 11 });

  codeBox(s,
    "ulong whitePawns =\n  0b00000000\n    00000000\n    00000000\n    00000000\n    00000000\n    00000000\n    11111111  ← rank 2 (set)\n    00000000;",
    6.55, 2.7, 6.6, 2.0);

  label(s, "Decimal value:  65,280", 6.7, 4.78, 5.5, 0.3, { mono: true, size: 11, color: C.yellow });

  const bits = [
    { label: "Bit SET (1)", color: C.accent },
    { label: "Bit CLEAR (0)", color: "21262D" },
  ];
  bits.forEach((b, i) => {
    s.addShape(prs.ShapeType.rect, { x: 6.7 + i * 2.8, y: 5.25, w: 0.28, h: 0.28, fill: { color: b.color }, line: { color: C.border } });
    label(s, b.label, 6.7 + i * 2.8 + 0.36, 5.22, 2.3, 0.32, { size: 11, color: C.dim });
  });

  label(s, "The engine has 12 of these bitboards — one per piece type per colour.", 6.7, 5.62, 6.5, 0.5, { size: 11, color: C.dim });
}

// ─────────────────────────────────────────────────────────────────────────────
// SLIDE 3 – The 12 Bitboards
// ─────────────────────────────────────────────────────────────────────────────
{
  const s = slide("The 12 Bitboards", "Every piece type for both colours gets its own 64-bit integer");

  const pieces = [
    { name: "WhitePawns",   sym: "♙", color: C.accent,  val: "0x000000000000FF00" },
    { name: "WhiteKnights", sym: "♘", color: C.accent,  val: "0x0000000000000042" },
    { name: "WhiteBishops", sym: "♗", color: C.accent,  val: "0x0000000000000024" },
    { name: "WhiteRooks",   sym: "♖", color: C.accent,  val: "0x0000000000000081" },
    { name: "WhiteQueens",  sym: "♕", color: C.accent,  val: "0x0000000000000008" },
    { name: "WhiteKing",    sym: "♔", color: C.accent,  val: "0x0000000000000010" },
    { name: "BlackPawns",   sym: "♟", color: C.accent2, val: "0x00FF000000000000" },
    { name: "BlackKnights", sym: "♞", color: C.accent2, val: "0x4200000000000000" },
    { name: "BlackBishops", sym: "♝", color: C.accent2, val: "0x2400000000000000" },
    { name: "BlackRooks",   sym: "♜", color: C.accent2, val: "0x8100000000000000" },
    { name: "BlackQueens",  sym: "♛", color: C.accent2, val: "0x0800000000000000" },
    { name: "BlackKing",    sym: "♚", color: C.accent2, val: "0x1000000000000000" },
  ];

  pieces.forEach((p, i) => {
    const col = i % 3;
    const row = Math.floor(i / 3);
    const x = 0.4 + col * 4.35;
    const y = 1.35 + row * 1.38;
    panel(s, x, y, 4.15, 1.26);
    s.addText(p.sym, { x: x + 0.12, y: y + 0.1, w: 0.75, h: 1.0, fontSize: 38, color: p.color, align: "center", valign: "middle" });
    label(s, p.name, x + 0.95, y + 0.15, 3.0, 0.32, { bold: true, size: 13, color: p.color });
    label(s, p.val, x + 0.95, y + 0.5, 3.1, 0.26, { mono: true, size: 9.5, color: C.dim });
    // mini 8×8 dot grid
    for (let r = 0; r < 8; r++) {
      for (let c = 0; c < 8; c++) {
        const isWhite = i < 6;
        const filled = isWhite ? r === 6 : r === 1; // simplified visual
        s.addShape(prs.ShapeType.rect, {
          x: x + 0.95 + c * 0.115, y: y + 0.8 + r * 0.048,
          w: 0.1, h: 0.04,
          fill: { color: filled ? p.color : "1C2128" },
          line: { type: "none" },
        });
      }
    }
  });

  // bottom note
  label(s, "OR all 12 together with | (bitwise OR) to get OccupiedSquares — used for collision detection in one CPU instruction.",
    0.4, 6.9, 12.8, 0.45, { size: 11.5, color: C.dim });
}

// ─────────────────────────────────────────────────────────────────────────────
// SLIDE 4 – Bitwise Move Generation
// ─────────────────────────────────────────────────────────────────────────────
{
  const s = slide("Bitwise Move Generation", "Shifting bits replaces slow loops — the engine uses CPU bitwise instructions");

  // Left explanation
  panel(s, 0.4, 1.3, 6.0, 5.3);
  label(s, "White Pawn Push (1 square forward)", 0.65, 1.45, 5.5, 0.32, { bold: true, size: 13, color: C.accent });
  label(s, '"Forward" for white = shifting bits LEFT by 8 positions\n(one rank = 8 bits in the integer)', 0.65, 1.82, 5.4, 0.5, { size: 11, color: C.dim });

  codeBox(s,
    "// White pawn single push\nulong singlePush =\n  (whitePawns << 8) & ~occupiedSquares;\n\n// Double push from rank 2\nulong doublePush =\n  ((singlePush & rank3Mask) << 8)\n  & ~occupiedSquares;\n\n// Captures (diagonal, left & right)\nulong captureLeft  = (whitePawns << 7) & blackPieces;\nulong captureRight = (whitePawns << 9) & blackPieces;",
    0.5, 2.35, 5.7, 2.9);

  label(s, "No loops. No board scanning.\nOne shift + one AND = all legal pawn moves.", 0.65, 5.3, 5.4, 0.6, { size: 11, color: C.green });

  // Right visual — show the shift
  panel(s, 6.65, 1.3, 6.6, 5.3);
  label(s, "Visual: pawn on d2 → d4 (double push)", 6.9, 1.45, 6.1, 0.32, { bold: true, size: 13, color: C.white });

  const gx = 6.9, gy = 1.85, sq = 0.58;
  const d2 = 11; // rank 6 (from top, 0-indexed), file 3
  const d3 = 19;
  const d4 = 27; // bit indices if reading top-to-bottom

  for (let r = 0; r < 8; r++) {
    for (let c = 0; c < 8; c++) {
      const idx = r * 8 + c;
      let fillColor = (r + c) % 2 === 0 ? "21262D" : "161B22";
      let sym = "";
      let symColor = C.white;
      if (idx === d2)  { fillColor = C.accent; sym = "♙"; symColor = C.bg; }
      if (idx === d4)  { fillColor = "1B3A1B"; sym = "↑"; symColor = C.green; }
      if (idx === d3)  { fillColor = "1B2E3A"; sym = "·"; symColor = C.accent2; }

      s.addShape(prs.ShapeType.rect, {
        x: gx + c * sq, y: gy + r * sq, w: sq, h: sq,
        fill: { color: fillColor }, line: { color: C.border, width: 0.5 },
      });
      if (sym) s.addText(sym, { x: gx + c * sq, y: gy + r * sq, w: sq, h: sq, fontSize: sym === "♙" ? 20 : 16, align: "center", valign: "middle", color: symColor });
    }
  }
  // labels
  "abcdefgh".split("").forEach((f, i) => {
    s.addText(f, { x: gx + i * sq, y: gy + 8 * sq + 0.04, w: sq, h: 0.22, fontSize: 9, color: C.dim, align: "center" });
  });

  // Annotation
  s.addText("d2 (source)", { x: gx + 3 * sq + 0.62, y: gy + 6 * sq + 0.1, w: 2, h: 0.28, fontSize: 10, color: C.accent });
  s.addText("d4 (target)", { x: gx + 3 * sq + 0.62, y: gy + 4 * sq + 0.1, w: 2, h: 0.28, fontSize: 10, color: C.green });

  // bit math
  codeBox(s, "Bit 11 (d2) << 16 = Bit 27 (d4)\n0x800  << 16 = 0x8000000", 6.65, 5.45, 6.6, 0.75);
}

// ─────────────────────────────────────────────────────────────────────────────
// SLIDE 5 – Zobrist Hashing
// ─────────────────────────────────────────────────────────────────────────────
{
  const s = slide("Zobrist Hashing", "The engine gives every unique board position a fingerprint — a 64-bit hash");

  // Why
  panel(s, 0.4, 1.3, 5.5, 1.3);
  label(s, "Why?", 0.65, 1.4, 5, 0.3, { bold: true, size: 13, color: C.yellow });
  label(s, "The engine searches the same position many times through different move orders.\nThe hash lets it instantly recognise: \"I've seen this before\" and skip the work.",
    0.65, 1.72, 5.1, 0.75, { size: 11, color: C.dim });

  // How
  panel(s, 6.15, 1.3, 7.1, 1.3);
  label(s, "How it's built", 6.4, 1.4, 6.5, 0.3, { bold: true, size: 13, color: C.accent2 });
  label(s, "At startup, generate 768 random 64-bit numbers (12 piece types × 64 squares).\nFor each piece on the board, XOR its random number into the hash.",
    6.4, 1.72, 6.7, 0.75, { size: 11, color: C.dim });

  // XOR table
  panel(s, 0.4, 2.75, 12.85, 2.15);
  label(s, "Building the hash for the starting position (excerpt):", 0.65, 2.88, 10, 0.3, { bold: true, size: 12, color: C.white });

  const rows = [
    ["Piece",               "Square",  "Random Key (64-bit)",         "XOR result"],
    ["White King  ♔",       "e1",      "0xA1B2C3D4E5F60708",          "0xA1B2C3D4E5F60708"],
    ["White Queen ♕",       "d1",      "0x3F8E1A2B4C5D6E7F",          "0x9E3CDF7EA9A96177"],
    ["White Pawn  ♙",       "d2",      "0x0102030405060708",          "0x9F3EDC7EACAF6677"],
    ["… (all 32 pieces)",  "…",       "…",                            "→ FINAL HASH"],
  ];

  rows.forEach((row, ri) => {
    row.forEach((cell, ci) => {
      const cx = [0.55, 2.45, 5.15, 9.5];
      const cw = [1.8, 2.6, 4.25, 3.6];
      s.addText(cell, {
        x: cx[ci], y: 3.2 + ri * 0.32, w: cw[ci], h: 0.3,
        fontSize: ri === 0 ? 10.5 : 10,
        bold: ri === 0,
        color: ri === 0 ? C.dim : (ci === 3 ? C.green : C.white),
        fontFace: ri > 0 && ci >= 2 ? "Consolas" : "Segoe UI",
      });
    });
  });

  // Incremental update
  panel(s, 0.4, 5.05, 12.85, 1.6);
  label(s, "Incremental Update — O(1) cost per move", 0.65, 5.18, 10, 0.3, { bold: true, size: 13, color: C.yellow });
  codeBox(s,
    "// You moved pawn from d2 → d4:\nhash ^= ZobristTable[WhitePawn, d2];   // XOR removes pawn from d2\nhash ^= ZobristTable[WhitePawn, d4];   // XOR adds pawn on d4\n// No need to rehash 32 pieces — just 2 XOR operations!",
    0.5, 5.48, 12.6, 1.05);
}

// ─────────────────────────────────────────────────────────────────────────────
// SLIDE 6 – The Transposition Table
// ─────────────────────────────────────────────────────────────────────────────
{
  const s = slide("Transposition Table", "The engine's long-term memory — keyed by Zobrist hash");

  panel(s, 0.4, 1.3, 12.85, 1.1);
  label(s, "d4 → Nf6  and  Nf6 → d4  reach the same board.  Without a table the engine recalculates it twice.\nWith the table it recognises the hash, loads the cached result, and skips the entire subtree.",
    0.65, 1.4, 12.3, 0.85, { size: 12.5, color: C.dim });

  // Table structure
  label(s, "Entry structure:", 0.5, 2.6, 5, 0.3, { bold: true, size: 13, color: C.white });

  const fields = [
    { field: "ZobristKey",  type: "ulong",  desc: "Full 64-bit hash to verify the entry is correct (avoids collisions)" },
    { field: "BestMove",    type: "Move",   desc: "The best move found for this position — used for Move Ordering" },
    { field: "Score",       type: "int",    desc: "Evaluation score in centipawns at the time of storage" },
    { field: "Depth",       type: "int",    desc: "How deep the search was when this entry was created" },
    { field: "NodeType",    type: "enum",   desc: "Exact score / Lower bound (Beta) / Upper bound (Alpha)" },
    { field: "Age",         type: "byte",   desc: "Game move counter — old entries are overwritten first" },
  ];

  fields.forEach((f, i) => {
    const y = 2.95 + i * 0.55;
    panel(s, 0.4, y, 12.85, 0.5, i % 2 === 0 ? "161B22" : "0D1117");
    s.addText(f.field, { x: 0.6, y, w: 2.2, h: 0.5, fontSize: 11, color: C.accent2, fontFace: "Consolas", valign: "middle" });
    s.addText(f.type,  { x: 2.9, y, w: 1.3, h: 0.5, fontSize: 11, color: C.yellow, fontFace: "Consolas", valign: "middle" });
    s.addText(f.desc,  { x: 4.3, y, w: 8.7, h: 0.5, fontSize: 11, color: C.dim, fontFace: "Segoe UI", valign: "middle" });
  });

  codeBox(s,
    "// Probe at start of every node:\nvar entry = _ttable[hash % Size];\nif (entry.Key == hash && entry.Depth >= depth)\n    return entry.Score;  // ← instant answer, skips millions of nodes",
    0.4, 6.35, 12.85, 1.1);
}

// ─────────────────────────────────────────────────────────────────────────────
// SLIDE 7 – Iterative Deepening
// ─────────────────────────────────────────────────────────────────────────────
{
  const s = slide("Iterative Deepening", "Start shallow, go deeper — and always have an answer ready when time runs out");

  // Timeline
  const depths = [
    { d: 1, t: "< 1ms",   moves: 20,        color: C.green },
    { d: 2, t: "~1ms",    moves: 400,        color: C.green },
    { d: 3, t: "~5ms",    moves: "8,000",    color: C.green },
    { d: 4, t: "~40ms",   moves: "160k",     color: C.yellow },
    { d: 5, t: "~200ms",  moves: "3.2M",     color: C.yellow },
    { d: 6, t: "~1s",     moves: "64M",      color: C.accent },
    { d: 7, t: "~5s",     moves: "1.28B",    color: C.accent },
    { d: 8, t: "TIMEOUT", moves: "25.6B",    color: "3D1F1F" },
  ];

  depths.forEach((dep, i) => {
    const x = 0.4 + i * 1.62;
    const barH = 0.25 + i * 0.45;
    const by = 5.5 - barH;

    // bar
    s.addShape(prs.ShapeType.rect, { x: x + 0.08, y: by, w: 1.3, h: barH, fill: { color: dep.color }, line: { type: "none" } });

    // depth label on top
    s.addText(`D${dep.d}`, { x, y: by - 0.36, w: 1.46, h: 0.32, fontSize: 15, bold: true, color: dep.d === 8 ? C.accent : C.white, align: "center" });

    // time label
    s.addText(dep.t, { x, y: by - 0.65, w: 1.46, h: 0.28, fontSize: 9, color: C.dim, align: "center" });

    // nodes label inside bar
    if (i > 1) {
      s.addText(String(dep.moves), { x, y: by + 0.08, w: 1.46, h: barH - 0.1, fontSize: 8.5, color: C.bg, align: "center", valign: "top", bold: true });
    }
  });

  // divider line
  s.addShape(prs.ShapeType.line, { x: 0.4, y: 5.52, w: 12.5, h: 0, line: { color: C.border, width: 1 } });

  panel(s, 0.4, 5.65, 6.0, 1.5);
  label(s, "Why not just search Depth 8 directly?", 0.65, 5.75, 5.5, 0.3, { bold: true, size: 12.5, color: C.accent });
  label(s,
    "Because Depth 7 best-move seeds Depth 8 move ordering.\nAnd if time runs out mid-Depth-8, you already have a\nperfect Depth 7 answer to play — never waste a turn.",
    0.65, 6.08, 5.4, 1.0, { size: 11, color: C.dim });

  panel(s, 6.55, 5.65, 6.7, 1.5);
  codeBox(s,
    "for (int depth = 1; depth <= MAX_DEPTH; depth++) {\n  bestMove = Search(depth, -∞, +∞);\n  if (TimerExpired()) break;\n}\nreturn bestMove; // always from last COMPLETE depth",
    6.55, 5.65, 6.7, 1.5);
}

// ─────────────────────────────────────────────────────────────────────────────
// SLIDE 8 – Alpha-Beta Pruning
// ─────────────────────────────────────────────────────────────────────────────
{
  const s = slide("Alpha-Beta Pruning", "Throw away branches you don't need to look at — the single most important optimization");

  // Explanation panel
  panel(s, 0.4, 1.3, 5.3, 2.0);
  label(s, "Alpha (α) — Best score White can guarantee", 0.65, 1.45, 4.9, 0.3, { bold: true, size: 12, color: C.accent });
  label(s, "Beta  (β) — Best score Black can guarantee", 0.65, 1.78, 4.9, 0.3, { bold: true, size: 12, color: C.accent2 });
  label(s,
    "If the engine finds a move so bad that the opponent\nwould never allow it (score ≥ β), it stops looking\nat siblings.  That subtree is pruned.",
    0.65, 2.12, 4.9, 0.85, { size: 11, color: C.dim });

  // Tree visual
  const nodes = [
    { x: 6.5,  y: 1.35, label: "Root",   score: "",    color: C.panel,   text: "Root" },
    { x: 4.2,  y: 2.45, label: "A",      score: "5",   color: C.panel,   text: "A=5" },
    { x: 9.5,  y: 2.45, label: "B",      score: "3",   color: C.panel,   text: "B=3" },
    { x: 2.8,  y: 3.6,  label: "A1",     score: "5",   color: C.green,   text: "5" },
    { x: 5.3,  y: 3.6,  label: "A2",     score: "3",   color: C.green,   text: "3" },
    { x: 8.3,  y: 3.6,  label: "B1",     score: "9",   color: C.accent,  text: "9!" },
    { x: 11.0, y: 3.6,  label: "B2",     score: "?",   color: C.pruned,  text: "✗" },
  ];

  const lines = [
    [0,1],[0,2],[1,3],[1,4],[2,5],[2,6],
  ];

  lines.forEach(([a, b]) => {
    const na = nodes[a], nb = nodes[b];
    s.addShape(prs.ShapeType.line, {
      x: na.x + 0.45, y: na.y + 0.45,
      w: nb.x - na.x, h: nb.y - na.y,
      line: { color: nb.color === C.pruned ? "3D1F1F" : C.border, width: 1.5, dashType: nb.color === C.pruned ? "dash" : "solid" },
    });
  });

  nodes.forEach((n) => {
    s.addShape(prs.ShapeType.roundRect, {
      x: n.x, y: n.y, w: 0.9, h: 0.45,
      fill: { color: n.color }, line: { color: n.color === C.pruned ? "FF0000" : C.border, width: 1 },
      rectRadius: 0.06,
    });
    s.addText(n.text, { x: n.x, y: n.y, w: 0.9, h: 0.45, fontSize: 12, bold: true, color: n.color === C.pruned ? "FF6666" : C.white, align: "center", valign: "middle" });
  });

  // annotation for pruned node
  s.addText("PRUNED — B1 returned 9.\nWhite already has A=5.\nBlack would choose B1≥9 over B.\nWhite prefers A.  B2 is irrelevant.",
    { x: 10.0, y: 4.15, w: 3.2, h: 1.1, fontSize: 10, color: C.dim, wrap: true });

  // stat callout
  panel(s, 0.4, 4.6, 5.3, 1.1);
  label(s, "Effective branching factor drops from ~35 to ~6", 0.65, 4.72, 4.8, 0.3, { bold: true, size: 12.5, color: C.green });
  label(s, "With perfect move ordering, Alpha-Beta searches the SQUARE ROOT\nof the nodes a brute-force search would need.", 0.65, 5.02, 4.9, 0.6, { size: 11, color: C.dim });

  codeBox(s,
    "int AlphaBeta(int depth, int alpha, int beta) {\n  foreach (Move m in moves) {\n    int score = -AlphaBeta(depth-1, -beta, -alpha);\n    if (score >= beta)  return beta;  // ← CUT-OFF\n    if (score > alpha)  alpha = score;\n  }\n  return alpha;\n}",
    0.4, 5.78, 12.85, 1.45);
}

// ─────────────────────────────────────────────────────────────────────────────
// SLIDE 9 – Move Ordering
// ─────────────────────────────────────────────────────────────────────────────
{
  const s = slide("Move Ordering", "Alpha-Beta only prunes if you look at the best moves first — ordering is everything");

  const layers = [
    { rank: 1, label: "Hash Move",          desc: "Best move from the Transposition Table for this exact position",  color: C.accent },
    { rank: 2, label: "Winning Captures",   desc: "Captures where you gain material (SEE score > 0)  e.g. PxQ",     color: C.accent },
    { rank: 3, label: "Killer Moves",       desc: "Non-captures that caused a cut-off at this depth in sibling nodes", color: C.yellow },
    { rank: 4, label: "Equal Captures",     desc: "Captures where material gained = material lost  e.g. PxP",        color: C.dim },
    { rank: 5, label: "Quiet Moves",        desc: "Non-capturing moves, sorted by History Heuristic score",          color: C.white },
    { rank: 6, label: "Losing Captures",    desc: "Captures where you lose material (SEE score < 0)  e.g. QxP",      color: "FF6666" },
  ];

  layers.forEach((l, i) => {
    const y = 1.32 + i * 0.87;
    panel(s, 0.4, y, 0.7, 0.75, l.color);
    s.addText(String(l.rank), { x: 0.4, y, w: 0.7, h: 0.75, fontSize: 22, bold: true, color: C.bg, align: "center", valign: "middle" });
    panel(s, 1.2, y, 11.9, 0.75);
    label(s, l.label, 1.42, y + 0.04, 3.8, 0.3, { bold: true, size: 13, color: l.color });
    label(s, l.desc,  1.42, y + 0.36, 11.3, 0.32, { size: 11, color: C.dim });
  });
}

// ─────────────────────────────────────────────────────────────────────────────
// SLIDE 10 – Static Exchange Evaluation (SEE)
// ─────────────────────────────────────────────────────────────────────────────
{
  const s = slide("Static Exchange Evaluation (SEE)", "Is this capture actually profitable? Simulate the full exchange before committing");

  panel(s, 0.4, 1.3, 12.85, 1.0);
  label(s, "When a White Knight captures a Black Queen, is that a good trade?\nSEE simulates every recapture on that square to give a material gain/loss score — without doing a full search.",
    0.65, 1.4, 12.3, 0.8, { size: 12.5, color: C.dim });

  // Exchange sequence
  const exchange = [
    { move: "NxQ",   gain: "+900",  total: "+900", color: C.green },
    { move: "PxN",   gain: "-300",  total: "+600", color: C.yellow },
    { move: "BxP",   gain: "+100",  total: "+700", color: C.green },
    { move: "NxB",   gain: "-300",  total: "+400", color: C.yellow },
    { move: "---",   gain: "stop",  total: "+400", color: C.panel },
  ];

  label(s, "Example: Knight captures Queen on d5", 0.5, 2.45, 8, 0.3, { bold: true, size: 13, color: C.white });

  exchange.forEach((e, i) => {
    const x = 0.4 + i * 2.55;
    panel(s, x, 2.82, 2.3, 1.5, e.color === C.panel ? C.panel : "161B22");
    s.addShape(prs.ShapeType.rect, { x, y: 2.82, w: 2.3, h: 0.12, fill: { color: e.color }, line: { type: "none" } });
    label(s, e.move,  x + 0.12, 3.0,  2.0, 0.35, { bold: true, size: 16, color: C.white });
    label(s, e.gain,  x + 0.12, 3.38, 2.0, 0.3,  { size: 12, color: e.gain.startsWith("+") ? C.green : C.accent });
    label(s, `Net: ${e.total}`, x + 0.12, 3.7, 2.0, 0.5, { size: 10.5, color: C.yellow });
  });

  label(s, "SEE result = +400 centipawns (4 pawns ahead) → this is a WINNING capture", 0.5, 4.45, 12, 0.32, { bold: true, size: 13, color: C.green });

  codeBox(s,
    "int SEE(Square toSquare, Piece captured, Square fromSquare, Piece attacker) {\n  int gain = PieceValue[captured];\n  if (gain - SEE(toSquare, attacker, nextAttacker) > 0)\n      return gain - SEE(...);   // recapture is worthwhile\n  return 0;                       // stop — don't recapture\n}",
    0.4, 4.9, 12.85, 1.35);

  panel(s, 0.4, 6.35, 12.85, 0.85);
  label(s, "SEE score > 0  →  Winning capture  →  Rank 2 in move ordering\nSEE score < 0  →  Losing capture   →  Rank 6 in move ordering (searched last or pruned)", 0.65, 6.42, 12.3, 0.72, { size: 11.5, color: C.dim });
}

// ─────────────────────────────────────────────────────────────────────────────
// SLIDE 11 – Advanced Pruning Techniques
// ─────────────────────────────────────────────────────────────────────────────
{
  const s = slide("Advanced Pruning Techniques", "Extra heuristics that skip large parts of the search tree safely");

  const techniques = [
    {
      name: "Null Move Pruning",
      color: C.accent,
      what: "Skip your turn and search at reduced depth. If the position is still great, the original move is likely a cut-off.",
      when: "Not in check, not in zugzwang, depth ≥ 3",
      code: "// Give opponent two moves in a row\nscore = -Search(depth - R - 1, -beta, -beta+1);\nif (score >= beta) return beta; // NMP cut",
    },
    {
      name: "Razoring",
      color: C.yellow,
      what: "At low depths, if the static evaluation is far below alpha, skip the full search — jump to quiescence only.",
      when: "depth == 1 or 2, score + margin < alpha",
      code: "if (depth <= 2 && staticEval + Margin[depth] < alpha)\n  return QuiescenceSearch(alpha, beta);",
    },
    {
      name: "Late Move Reductions (LMR)",
      color: C.accent2,
      what: "Later moves in the sorted list are probably bad. Search them at reduced depth first. Only re-search at full depth if they beat alpha.",
      when: "Move index ≥ 4, not a capture, not in check",
      code: "int R = reduction(depth, moveIndex);\nscore = -Search(depth - R - 1, -alpha-1, -alpha);\nif (score > alpha) // re-search at full depth\n  score = -Search(depth - 1, -beta, -alpha);",
    },
  ];

  techniques.forEach((t, i) => {
    const y = 1.32 + i * 1.92;
    panel(s, 0.4, y, 12.85, 1.85);
    s.addShape(prs.ShapeType.rect, { x: 0.4, y, w: 0.12, h: 1.85, fill: { color: t.color }, line: { type: "none" } });
    label(s, t.name, 0.65, y + 0.1, 7, 0.35, { bold: true, size: 15, color: t.color });
    label(s, t.what, 0.65, y + 0.47, 5.5, 0.6, { size: 11, color: C.dim });
    label(s, `When: ${t.when}`, 0.65, y + 1.05, 5.5, 0.3, { size: 10, color: C.yellow });
    codeBox(s, t.code, 6.55, y + 0.12, 6.6, 1.6);
  });
}

// ─────────────────────────────────────────────────────────────────────────────
// SLIDE 12 – Quiescence Search
// ─────────────────────────────────────────────────────────────────────────────
{
  const s = slide("Quiescence Search", "Never evaluate a position mid-capture — the horizon effect would mislead scoring");

  panel(s, 0.4, 1.3, 12.85, 1.05);
  label(s,
    "The engine reaches its depth limit and is about to call Evaluate().  But a pawn just captured a queen.\nThe static evaluator sees the pawn and says \"great position!\" — not knowing the queen is about to be recaptured.\nQuiescence Search keeps looking at captures until the board is \"quiet\".",
    0.65, 1.38, 12.3, 0.9, { size: 12, color: C.dim });

  // Visual: noisy vs quiet
  const scenarios = [
    { title: "WITHOUT Quiescence", sub: "Engine stops at depth 7 mid-battle", bad: true, eval: "+9.0  ← WRONG (pawn took queen)" },
    { title: "WITH Quiescence",    sub: "Engine extends until no captures remain", bad: false, eval: "-0.2  ← CORRECT (queen was recaptured)" },
  ];

  scenarios.forEach((sc, i) => {
    const x = 0.4 + i * 6.6;
    panel(s, x, 2.5, 6.3, 3.0, sc.bad ? "1F1010" : "0F1F10");
    s.addShape(prs.ShapeType.rect, { x, y: 2.5, w: 6.3, h: 0.1, fill: { color: sc.bad ? C.accent : C.green }, line: { type: "none" } });
    label(s, sc.title, x + 0.2, 2.65, 5.8, 0.35, { bold: true, size: 14, color: sc.bad ? C.accent : C.green });
    label(s, sc.sub,   x + 0.2, 3.02, 5.8, 0.3,  { size: 11, color: C.dim });
    s.addText(sc.bad ? "🌩" : "✓", { x: x + 0.2, y: 3.38, w: 0.6, h: 0.6, fontSize: 28, align: "center" });
    label(s, sc.eval,  x + 0.9, 3.4, 5.0, 0.6, { bold: true, size: 15, color: sc.bad ? C.accent : C.green });
    label(s, sc.bad
      ? "Evaluator sees the pawn on queen's square\nbefore recapture — vastly inflates the score"
      : "Plays out: PxQ, RxP, PxR... until no more\ncaptures available. Scores the final quiet board",
      x + 0.2, 3.98, 5.8, 0.75, { size: 11, color: C.dim });
  });

  codeBox(s,
    "int QuiescenceSearch(int alpha, int beta) {\n  int standPat = Evaluate();           // static eval of current position\n  if (standPat >= beta) return beta;   // position is already great — stop\n  alpha = Max(alpha, standPat);\n  foreach (Move capture in captures) { // only look at captures\n    int score = -QuiescenceSearch(-beta, -alpha);\n    if (score >= beta) return beta;\n    alpha = Max(alpha, score);\n  }\n  return alpha;                        // return when no captures remain\n}",
    0.4, 5.6, 12.85, 1.85);
}

// ─────────────────────────────────────────────────────────────────────────────
// SLIDE 13 – Static Evaluation
// ─────────────────────────────────────────────────────────────────────────────
{
  const s = slide("Static Evaluation", "Counting who's actually winning — the engine's judge at the leaf nodes");

  const components = [
    { name: "Material",       weight: "+++++", desc: "Count each piece × its value (P=100, N=300, B=325, R=500, Q=900)",  color: C.accent },
    { name: "Piece-Square Tables", weight: "++++",  desc: "A pawn on e4 is worth more than d7 — 64 bonus values per piece type", color: C.accent2 },
    { name: "Pawn Structure",      weight: "+++",   desc: "Passed pawns (+), doubled pawns (−), isolated pawns (−)",          color: C.yellow },
    { name: "King Safety",         weight: "+++",   desc: "Pawn shield score, open files near king (−), attackers near king (−)", color: C.green },
    { name: "Mobility",            weight: "++",    desc: "More legal moves = better development; subtract opponent mobility",   color: C.dim },
    { name: "Bishop Pair",         weight: "+",     desc: "+50 centipawns if you have both bishops (strong in open games)",       color: C.dim },
  ];

  components.forEach((c, i) => {
    const y = 1.32 + i * 0.88;
    const barW = (c.weight.length / 5) * 2.5;
    panel(s, 0.4, y, 12.85, 0.8);
    s.addShape(prs.ShapeType.rect, { x: 0.4, y, w: barW, h: 0.8, fill: { color: "1C2030" }, line: { type: "none" } });
    label(s, c.name,   0.65, y + 0.06, 2.6, 0.32, { bold: true, size: 13, color: c.color });
    label(s, c.weight, 3.4,  y + 0.06, 1.6, 0.32, { mono: true, size: 13, color: c.color });
    label(s, c.desc,   5.1,  y + 0.06, 7.9, 0.65, { size: 11, color: C.dim });
  });

  codeBox(s,
    "// Returns score in centipawns from White's POV\n// Positive = White winning, Negative = Black winning\nint Evaluate(Board b) {\n  return MaterialScore(b) + PieceSquareScore(b) + PawnScore(b)\n       + KingSafetyScore(b) + MobilityScore(b) + BishopPairBonus(b);\n}",
    0.4, 6.72, 12.85, 1.0);
}

// ─────────────────────────────────────────────────────────────────────────────
// SLIDE 14 – The Full Loop
// ─────────────────────────────────────────────────────────────────────────────
{
  const s = slide("The Full Loop", "Everything the engine does from the moment you play d4");

  const steps = [
    { n: "1", label: "You play d4",              sub: "React frontend drops the pawn. POST /game/move sent to API.", color: C.white },
    { n: "2", label: "Board Updated",             sub: "C# updates bitboards. Zobrist hash updated in 2 XOR ops.", color: C.accent },
    { n: "3", label: "FindBestMove() called",     sub: "Engine wipes killer moves. 5-second clock starts.", color: C.accent },
    { n: "4", label: "Iterative Deepening loop",  sub: "Depth 1 → 2 → 3 … Seed each depth with last depth's best move.", color: C.yellow },
    { n: "5", label: "AlphaBeta called",          sub: "Probe TT. Generate moves. Order by hash/captures/killers.", color: C.yellow },
    { n: "6", label: "Pruning fires",             sub: "NMP, Razoring, LMR, Alpha-Beta cuts discard millions of nodes.", color: C.green },
    { n: "7", label: "Leaf reached → QSearch",    sub: "Extend captures until quiet. Evaluate static score.", color: C.green },
    { n: "8", label: "Timer expires",             sub: "Current depth discarded. Best move from last complete depth selected.", color: C.accent },
    { n: "9", label: "Engine plays Nf6",          sub: "API returns move. React animates knight to f6. Your turn.", color: C.accent2 },
  ];

  const cols = 3;
  steps.forEach((st, i) => {
    const col = i % cols;
    const row = Math.floor(i / cols);
    const x = 0.4 + col * 4.38;
    const y = 1.32 + row * 1.78;
    panel(s, x, y, 4.2, 1.65);
    s.addShape(prs.ShapeType.rect, { x, y, w: 4.2, h: 0.1, fill: { color: st.color }, line: { type: "none" } });
    s.addText(st.n, { x: x + 0.12, y: y + 0.18, w: 0.55, h: 0.55, fontSize: 22, bold: true, color: st.color, align: "center", valign: "middle" });
    label(s, st.label, x + 0.75, y + 0.18, 3.3, 0.35, { bold: true, size: 12.5, color: C.white });
    label(s, st.sub,   x + 0.75, y + 0.56, 3.3, 0.88, { size: 10.5, color: C.dim });

    // arrow right (not last in row)
    if (col < cols - 1 && i < steps.length - 1) {
      s.addShape(prs.ShapeType.line, {
        x: x + 4.22, y: y + 0.82, w: 0.14, h: 0,
        line: { color: st.color, width: 1.5 },
      });
    }
  });
}

// ─────────────────────────────────────────────────────────────────────────────
// SLIDE 15 – Architecture Map
// ─────────────────────────────────────────────────────────────────────────────
{
  const s = slide("Project Architecture", "C# .NET 10 solution — four layers, clean separation");

  const boxes = [
    { x: 0.5,  y: 1.45, w: 2.8, h: 5.2, label: "chess-ui\n(React / TS)",    color: C.accent2, items: ["App.tsx", "gameApi.ts", "engineHub.ts", "react-chessboard"] },
    { x: 3.7,  y: 1.45, w: 2.8, h: 5.2, label: "Chess.API\n(ASP.NET 10)",    color: C.accent,  items: ["GameController.cs", "SignalR Hub", "appsettings.json", "Program.cs"] },
    { x: 6.9,  y: 1.45, w: 2.8, h: 5.2, label: "Chess.Engine.AI\n(Search)", color: C.yellow,  items: ["Search.cs", "MoveOrderer.cs", "TransposTable.cs", "Evaluation.cs"] },
    { x: 10.1, y: 1.45, w: 2.8, h: 5.2, label: "Chess.Engine.Core\n(Rules)",color: C.green,   items: ["Board.cs", "MoveGenerator.cs", "ZobristHash.cs", "Move.cs"] },
  ];

  const arrows = [
    { x1: 3.3, y1: 3.7, x2: 3.68, y2: 3.7, label: "HTTP\nREST" },
    { x1: 6.5, y1: 3.7, x2: 6.88, y2: 3.7, label: "C#\ncall" },
    { x1: 9.7, y1: 3.7, x2: 10.08, y2: 3.7, label: "C#\ncall" },
  ];

  boxes.forEach((b) => {
    panel(s, b.x, b.y, b.w, b.h);
    s.addShape(prs.ShapeType.rect, { x: b.x, y: b.y, w: b.w, h: 0.55, fill: { color: "1E2A1E" }, line: { type: "none" } });
    label(s, b.label, b.x + 0.15, b.y + 0.08, b.w - 0.2, 0.42, { bold: true, size: 11.5, color: b.color });
    b.items.forEach((item, i) => {
      s.addText("▸ " + item, { x: b.x + 0.15, y: b.y + 0.75 + i * 0.55, w: b.w - 0.2, h: 0.48, fontSize: 10, color: C.white, fontFace: "Consolas" });
    });
  });

  arrows.forEach((a) => {
    s.addShape(prs.ShapeType.line, { x: a.x1, y: a.y1, w: a.x2 - a.x1, h: 0, line: { color: C.border, width: 1.5 } });
    label(s, a.label, a.x1 + 0.02, a.y1 - 0.55, 0.56, 0.48, { size: 8.5, color: C.dim, align: "center" });
  });

  codeBox(s,
    "Chess.API  →  GameController  →  ChessEngine.FindBestMove(board, 5000ms)\n  Chess.Engine.AI.Search  →  AlphaBeta(depth, α, β)\n    Chess.Engine.Core.MoveGenerator  →  GenerateLegalMoves(bitboards)\n    Chess.Engine.Core.ZobristHash    →  ComputeHash(board)",
    0.5, 6.82, 12.7, 0.95);
}

// ─────────────────────────────────────────────────────────────────────────────
// Write the file
// ─────────────────────────────────────────────────────────────────────────────
prs.writeFile({ fileName: "../docs/ChessEngineArchitecture.pptx" })
  .then(() => console.log("✅  ChessEngineArchitecture.pptx written successfully"))
  .catch(err => { console.error("❌  Error:", err); process.exit(1); });
