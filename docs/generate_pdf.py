"""Generate a visual project overview PDF for Chess API."""

from reportlab.lib.pagesizes import A4
from reportlab.lib import colors
from reportlab.lib.units import mm
from reportlab.lib.styles import getSampleStyleSheet, ParagraphStyle
from reportlab.lib.enums import TA_CENTER, TA_LEFT, TA_RIGHT
from reportlab.platypus import (
    SimpleDocTemplate, Paragraph, Spacer, Table, TableStyle,
    HRFlowable, PageBreak, KeepTogether
)
from reportlab.platypus.flowables import Flowable
from reportlab.graphics.shapes import Drawing, Rect, String, Line, Circle, Polygon
from reportlab.graphics import renderPDF
from reportlab.graphics.shapes import Group
import os

# ── Colour palette ────────────────────────────────────────────
C_BG        = colors.HexColor("#0F172A")   # dark navy
C_PANEL     = colors.HexColor("#1E293B")
C_ACCENT    = colors.HexColor("#6366F1")   # indigo
C_GREEN     = colors.HexColor("#22C55E")
C_YELLOW    = colors.HexColor("#EAB308")
C_RED       = colors.HexColor("#EF4444")
C_BLUE      = colors.HexColor("#3B82F6")
C_CYAN      = colors.HexColor("#06B6D4")
C_ORANGE    = colors.HexColor("#F97316")
C_WHITE     = colors.HexColor("#F8FAFC")
C_MUTED     = colors.HexColor("#94A3B8")
C_BORDER    = colors.HexColor("#334155")

W, H = A4  # 595 x 842 pt

# ── Custom Flowable: coloured box with title + body ───────────
class InfoBox(Flowable):
    def __init__(self, title, lines, title_color=C_ACCENT, width=None, icon=None):
        super().__init__()
        self.title = title
        self.lines = lines
        self.title_color = title_color
        self.box_width = width or (W - 60)
        self.icon = icon
        self.hSpace = 12
        self.vPad = 8

    def wrap(self, avail_w, avail_h):
        self.box_width = min(self.box_width, avail_w)
        row_h = 14
        self.box_height = 28 + self.vPad * 2 + len(self.lines) * row_h
        return (self.box_width, self.box_height)

    def draw(self):
        c = self.canv
        w, h = self.box_width, self.box_height
        # background
        c.setFillColor(C_PANEL)
        c.setStrokeColor(C_BORDER)
        c.setLineWidth(0.5)
        c.roundRect(0, 0, w, h, 6, fill=1, stroke=1)
        # title bar
        c.setFillColor(self.title_color)
        c.roundRect(0, h - 28, w, 28, 6, fill=1, stroke=0)
        c.rect(0, h - 28, w, 14, fill=1, stroke=0)
        # title text
        c.setFillColor(C_WHITE)
        c.setFont("Helvetica-Bold", 10)
        c.drawString(self.hSpace, h - 18, self.title)
        # body lines
        c.setFont("Helvetica", 8.5)
        y = h - 28 - self.vPad - 10
        for line in self.lines:
            if line.startswith("•"):
                c.setFillColor(self.title_color)
                c.circle(self.hSpace + 3, y + 3, 2, fill=1, stroke=0)
                c.setFillColor(C_WHITE)
                c.drawString(self.hSpace + 10, y, line[1:].strip())
            elif line.startswith("→"):
                c.setFillColor(C_MUTED)
                c.drawString(self.hSpace, y, line)
            else:
                c.setFillColor(C_MUTED)
                c.drawString(self.hSpace, y, line)
            y -= 14


# ── Custom Flowable: architecture diagram ─────────────────────
class ArchDiagram(Flowable):
    def __init__(self, width=None):
        super().__init__()
        self.dw = width or (W - 60)
        self.dh = 210

    def wrap(self, aw, ah):
        self.dw = min(self.dw, aw)
        return (self.dw, self.dh)

    def _box(self, c, x, y, w, h, label, sublabel, fill, stroke=C_BORDER):
        c.setFillColor(fill)
        c.setStrokeColor(stroke)
        c.setLineWidth(1)
        c.roundRect(x, y, w, h, 5, fill=1, stroke=1)
        c.setFillColor(C_WHITE)
        c.setFont("Helvetica-Bold", 9)
        c.drawCentredString(x + w / 2, y + h / 2 + 3, label)
        c.setFont("Helvetica", 7.5)
        c.setFillColor(colors.HexColor("#CBD5E1"))
        c.drawCentredString(x + w / 2, y + h / 2 - 8, sublabel)

    def _arrow(self, c, x1, y1, x2, y2):
        c.setStrokeColor(C_MUTED)
        c.setLineWidth(1)
        c.line(x1, y1, x2, y2)
        # arrowhead
        import math
        angle = math.atan2(y2 - y1, x2 - x1)
        size = 5
        c.setFillColor(C_MUTED)
        p = c.beginPath()
        p.moveTo(x2, y2)
        p.lineTo(x2 - size * math.cos(angle - 0.4), y2 - size * math.sin(angle - 0.4))
        p.lineTo(x2 - size * math.cos(angle + 0.4), y2 - size * math.sin(angle + 0.4))
        p.close()
        c.drawPath(p, fill=1, stroke=0)

    def draw(self):
        c = self.canv
        w = self.dw
        bg = self.dh
        # background panel
        c.setFillColor(C_PANEL)
        c.setStrokeColor(C_BORDER)
        c.setLineWidth(0.5)
        c.roundRect(0, 0, w, bg, 8, fill=1, stroke=1)

        bw = (w - 80) / 4      # box width
        bh = 50
        gap = 20
        total = 4 * bw + 3 * gap
        startx = (w - total) / 2
        cy = bg / 2 - bh / 2 + 10

        boxes = [
            ("React UI", "TypeScript + Vite", C_BLUE),
            ("REST / SignalR", "ASP.NET Core 8", C_ACCENT),
            ("AI Engine", "Alpha-Beta Search", C_GREEN),
            ("Engine Core", "Bitboards + FEN", C_ORANGE),
        ]

        positions = []
        for i, (label, sub, col) in enumerate(boxes):
            bx = startx + i * (bw + gap)
            self._box(c, bx, cy, bw, bh, label, sub, col)
            positions.append((bx, cy, bw, bh))

        # arrows between boxes
        for i in range(len(positions) - 1):
            x1 = positions[i][0] + positions[i][2]
            y1 = positions[i][1] + positions[i][3] / 2
            x2 = positions[i + 1][0]
            y2 = positions[i + 1][1] + positions[i + 1][3] / 2
            self._arrow(c, x1, y1, x2, y2)

        # labels above
        labels_top = ["User", "HTTP / WebSocket", "Move + FEN", "Bitboard Ops"]
        c.setFont("Helvetica", 7)
        c.setFillColor(C_MUTED)
        for i, lbl in enumerate(labels_top):
            bx, _, bw2, _ = positions[i]
            if i < len(positions) - 1:
                mid = bx + bw2 + gap / 2
                c.drawCentredString(mid, cy + bh + 6, lbl)

        # legend row at bottom
        legend = [
            (C_BLUE, "Frontend"),
            (C_ACCENT, "API Layer"),
            (C_GREEN, "AI Search"),
            (C_ORANGE, "Core Engine"),
        ]
        lx = 20
        ly = 18
        c.setFont("Helvetica", 8)
        for col, name in legend:
            c.setFillColor(col)
            c.rect(lx, ly, 10, 10, fill=1, stroke=0)
            c.setFillColor(C_WHITE)
            c.drawString(lx + 14, ly + 1, name)
            lx += 90


# ── Custom Flowable: search algorithm flowchart ───────────────
class SearchFlowchart(Flowable):
    def __init__(self, width=None):
        super().__init__()
        self.dw = width or (W - 60)
        self.dh = 300

    def wrap(self, aw, ah):
        self.dw = min(self.dw, aw)
        return (self.dw, self.dh)

    def _node(self, c, x, y, w, h, text, fill, diamond=False):
        c.setFillColor(fill)
        c.setStrokeColor(C_BORDER)
        c.setLineWidth(0.8)
        if diamond:
            cx, cy2 = x + w / 2, y + h / 2
            p = c.beginPath()
            p.moveTo(cx, y + h)
            p.lineTo(x + w, cy2)
            p.lineTo(cx, y)
            p.lineTo(x, cy2)
            p.close()
            c.drawPath(p, fill=1, stroke=1)
        else:
            c.roundRect(x, y, w, h, 4, fill=1, stroke=1)
        c.setFillColor(C_WHITE)
        c.setFont("Helvetica-Bold" if not diamond else "Helvetica", 7.5)
        c.drawCentredString(x + w / 2, y + h / 2 - 3.5, text)

    def _line(self, c, x1, y1, x2, y2, label=""):
        c.setStrokeColor(C_MUTED)
        c.setLineWidth(0.8)
        c.line(x1, y1, x2, y2)
        if label:
            c.setFont("Helvetica", 6.5)
            c.setFillColor(C_YELLOW)
            mx, my = (x1 + x2) / 2, (y1 + y2) / 2
            c.drawCentredString(mx + 8, my, label)

    def draw(self):
        c = self.canv
        w = self.dw
        h = self.dh

        c.setFillColor(C_PANEL)
        c.setStrokeColor(C_BORDER)
        c.setLineWidth(0.5)
        c.roundRect(0, 0, w, h, 8, fill=1, stroke=1)

        cx = w / 2
        nw, nh = 160, 24
        dw2, dh2 = 160, 30   # diamond
        nx = cx - nw / 2

        nodes_y = [h - 30, h - 65, h - 100, h - 135, h - 170, h - 205, h - 240, h - 272]

        steps = [
            ("FindBestMove — Iterative Deepening",  C_ACCENT, False),
            ("Aspiration Window (±50 cp)",           C_BLUE,   False),
            ("AlphaBeta(depth, α, β, ply)",          C_ACCENT, False),
            ("TT probe — hit?",                      C_YELLOW, True),
            ("depth == 0?",                          C_YELLOW, True),
            ("Generate & Order Moves",               C_GREEN,  False),
            ("LMR / Futility Pruning",               C_ORANGE, False),
            ("β cut-off → store TT, return β",       C_RED,    False),
        ]

        for i, (text, fill, diamond) in enumerate(steps):
            y = nodes_y[i]
            if diamond:
                self._node(c, nx, y - dh2 / 2 + nh / 2, nw, dh2, text, fill, diamond=True)
            else:
                self._node(c, nx, y, nw, nh, text, fill)

        # vertical connectors
        for i in range(len(steps) - 1):
            y1 = nodes_y[i]
            y2 = nodes_y[i + 1]
            is_diamond_next = steps[i + 1][2]
            bot = y1
            top = y2 + nh
            self._line(c, cx, bot, cx, top)

        # side labels for decisions
        c.setFont("Helvetica", 6.5)
        c.setFillColor(C_GREEN)
        c.drawString(cx + nw / 2 + 4, nodes_y[3] + 2, "No → continue")
        c.setFillColor(C_RED)
        c.drawString(cx + nw / 2 + 4, nodes_y[3] - 12, "Yes → return score")

        c.setFillColor(C_GREEN)
        c.drawString(cx + nw / 2 + 4, nodes_y[4] + 2, "No → recurse")
        c.setFillColor(C_RED)
        c.drawString(cx + nw / 2 + 4, nodes_y[4] - 12, "Yes → Quiescence")

        # title
        c.setFont("Helvetica-Bold", 9)
        c.setFillColor(C_WHITE)
        c.drawCentredString(cx, h - 14, "Search Algorithm Flow")


# ── Custom Flowable: bit move diagram ─────────────────────────
class MoveBitDiagram(Flowable):
    def __init__(self, width=None):
        super().__init__()
        self.dw = width or (W - 60)
        self.dh = 70

    def wrap(self, aw, ah):
        self.dw = min(self.dw, aw)
        return (self.dw, self.dh)

    def draw(self):
        c = self.canv
        w = self.dw
        h = self.dh

        c.setFillColor(C_PANEL)
        c.setStrokeColor(C_BORDER)
        c.roundRect(0, 0, w, h, 6, fill=1, stroke=1)

        c.setFont("Helvetica-Bold", 9)
        c.setFillColor(C_WHITE)
        c.drawCentredString(w / 2, h - 14, "32-bit Move Encoding")

        fields = [
            ("Bits 0–5",   "From Square", C_BLUE),
            ("Bits 6–11",  "To Square",   C_GREEN),
            ("Bits 12–15", "Flag",         C_ACCENT),
            ("Bits 16–19", "Captured",     C_ORANGE),
            ("Bits 20–23", "Promotion",    C_RED),
        ]

        total_w = w - 40
        cell_w = total_w / len(fields)
        x0 = 20
        cy = h / 2 - 5

        for i, (bits, name, col) in enumerate(fields):
            bx = x0 + i * cell_w
            c.setFillColor(col)
            c.setStrokeColor(C_BORDER)
            c.roundRect(bx, cy - 10, cell_w - 4, 22, 3, fill=1, stroke=1)
            c.setFillColor(C_WHITE)
            c.setFont("Helvetica-Bold", 7)
            c.drawCentredString(bx + (cell_w - 4) / 2, cy + 3, name)
            c.setFont("Helvetica", 6)
            c.setFillColor(colors.HexColor("#CBD5E1"))
            c.drawCentredString(bx + (cell_w - 4) / 2, cy - 7, bits)


# ── Page templates ────────────────────────────────────────────
def cover_page(canvas, doc):
    canvas.saveState()
    # full-page dark background
    canvas.setFillColor(C_BG)
    canvas.rect(0, 0, W, H, fill=1, stroke=0)

    # accent bar top
    canvas.setFillColor(C_ACCENT)
    canvas.rect(0, H - 8, W, 8, fill=1, stroke=0)

    # chess king unicode glyph area (replaced with geometric chess symbol)
    cx, cy2 = W / 2, H / 2 + 60
    # crown base
    canvas.setFillColor(C_ACCENT)
    canvas.roundRect(cx - 35, cy2 - 30, 70, 50, 5, fill=1, stroke=0)
    canvas.setFillColor(C_BG)
    canvas.rect(cx - 25, cy2, 50, 20, fill=1, stroke=0)

    # title
    canvas.setFillColor(C_WHITE)
    canvas.setFont("Helvetica-Bold", 36)
    canvas.drawCentredString(W / 2, H * 0.55, "Chess API")

    canvas.setFont("Helvetica", 16)
    canvas.setFillColor(colors.HexColor("#CBD5E1"))
    canvas.drawCentredString(W / 2, H * 0.55 - 34, "Project Architecture & Design Reference")

    # horizontal rule
    canvas.setStrokeColor(C_ACCENT)
    canvas.setLineWidth(1.5)
    canvas.line(W / 2 - 100, H * 0.55 - 50, W / 2 + 100, H * 0.55 - 50)

    # subtitle pills
    pills = ["ASP.NET Core 8", "React + TypeScript", "Bitboard Engine", "SignalR"]
    pill_w, pill_h = 110, 22
    total = len(pills) * pill_w + (len(pills) - 1) * 10
    px = (W - total) / 2
    py = H * 0.55 - 90
    for pill in pills:
        canvas.setFillColor(C_PANEL)
        canvas.setStrokeColor(C_ACCENT)
        canvas.setLineWidth(0.8)
        canvas.roundRect(px, py, pill_w, pill_h, 11, fill=1, stroke=1)
        canvas.setFillColor(C_ACCENT)
        canvas.setFont("Helvetica-Bold", 8)
        canvas.drawCentredString(px + pill_w / 2, py + 7, pill)
        px += pill_w + 10

    # author / date
    canvas.setFont("Helvetica", 10)
    canvas.setFillColor(C_MUTED)
    canvas.drawCentredString(W / 2, 60, "Chess.API  •  Full-Stack Chess Application")
    canvas.drawCentredString(W / 2, 44, "2026")

    # accent bar bottom
    canvas.setFillColor(C_ACCENT)
    canvas.rect(0, 0, W, 6, fill=1, stroke=0)

    canvas.restoreState()


def normal_page(canvas, doc):
    canvas.saveState()
    canvas.setFillColor(C_BG)
    canvas.rect(0, 0, W, H, fill=1, stroke=0)
    # top bar
    canvas.setFillColor(C_PANEL)
    canvas.rect(0, H - 36, W, 36, fill=1, stroke=0)
    canvas.setFillColor(C_ACCENT)
    canvas.rect(0, H - 4, W, 4, fill=1, stroke=0)
    # header text
    canvas.setFont("Helvetica-Bold", 10)
    canvas.setFillColor(C_WHITE)
    canvas.drawString(30, H - 24, "Chess API")
    canvas.setFont("Helvetica", 9)
    canvas.setFillColor(C_MUTED)
    canvas.drawRightString(W - 30, H - 24, f"Page {doc.page}")
    # bottom border
    canvas.setFillColor(C_ACCENT)
    canvas.rect(0, 0, W, 3, fill=1, stroke=0)
    canvas.setFont("Helvetica", 7)
    canvas.setFillColor(C_MUTED)
    canvas.drawCentredString(W / 2, 10, "Chess.API — Project Reference")
    canvas.restoreState()


# ── Build document ────────────────────────────────────────────
def build_pdf(out_path):
    doc = SimpleDocTemplate(
        out_path, pagesize=A4,
        leftMargin=30, rightMargin=30,
        topMargin=50, bottomMargin=30,
    )

    styles = getSampleStyleSheet()
    h1 = ParagraphStyle("H1", fontName="Helvetica-Bold", fontSize=18,
                        textColor=C_WHITE, spaceAfter=6, spaceBefore=14)
    h2 = ParagraphStyle("H2", fontName="Helvetica-Bold", fontSize=13,
                        textColor=C_ACCENT, spaceAfter=4, spaceBefore=10)
    h3 = ParagraphStyle("H3", fontName="Helvetica-Bold", fontSize=10,
                        textColor=colors.HexColor("#CBD5E1"), spaceAfter=3, spaceBefore=6)
    body = ParagraphStyle("Body", fontName="Helvetica", fontSize=9,
                          textColor=C_MUTED, spaceAfter=3, leading=14)
    code_s = ParagraphStyle("Code", fontName="Courier", fontSize=8,
                            textColor=C_GREEN, spaceAfter=2, leading=12,
                            backColor=C_PANEL, leftIndent=8)

    def sp(n=8): return Spacer(1, n)
    def hr(): return HRFlowable(width="100%", thickness=0.5, color=C_BORDER, spaceAfter=6)

    story = []

    # ── Cover page (blank flowable, template does the drawing) ──
    story.append(PageBreak())  # triggers cover via onFirstPage

    # ── Page 2: Overview ────────────────────────────────────────
    story.append(Paragraph("Project Overview", h1))
    story.append(hr())
    story.append(Paragraph(
        "Chess API is a full-stack chess application with a custom bitboard chess engine, "
        "an ASP.NET Core REST/SignalR backend, and a React + TypeScript frontend. "
        "Players compete against an AI that uses iterative-deepening Alpha-Beta search "
        "with advanced pruning and real-time analysis streaming.", body))
    story.append(sp(10))

    story.append(Paragraph("System Architecture", h2))
    story.append(ArchDiagram(width=W - 60))
    story.append(sp(10))

    # Tech stack table
    story.append(Paragraph("Tech Stack", h2))
    ts_data = [
        ["Layer", "Technology", "Purpose"],
        ["Frontend", "React 19 + TypeScript + Vite", "Interactive chess board & analysis UI"],
        ["API", "ASP.NET Core 8 + SignalR", "REST endpoints & real-time move streaming"],
        ["AI Engine", "C# Alpha-Beta Search", "Move search with iterative deepening"],
        ["Core Engine", "C# Bitboards", "Board state, move generation, Zobrist hashing"],
        ["Testing", "xUnit 2.9", "Board, perft, evaluation & search unit tests"],
    ]
    ts_style = TableStyle([
        ("BACKGROUND",   (0, 0), (-1, 0),  C_ACCENT),
        ("TEXTCOLOR",    (0, 0), (-1, 0),  C_WHITE),
        ("FONTNAME",     (0, 0), (-1, 0),  "Helvetica-Bold"),
        ("FONTSIZE",     (0, 0), (-1, -1), 8.5),
        ("FONTNAME",     (0, 1), (-1, -1), "Helvetica"),
        ("TEXTCOLOR",    (0, 1), (-1, -1), C_MUTED),
        ("BACKGROUND",   (0, 1), (-1, -1), C_PANEL),
        ("ROWBACKGROUNDS", (0, 1), (-1, -1), [C_PANEL, colors.HexColor("#253347")]),
        ("GRID",         (0, 0), (-1, -1), 0.4, C_BORDER),
        ("LEFTPADDING",  (0, 0), (-1, -1), 8),
        ("RIGHTPADDING", (0, 0), (-1, -1), 8),
        ("TOPPADDING",   (0, 0), (-1, -1), 5),
        ("BOTTOMPADDING",(0, 0), (-1, -1), 5),
        ("BACKGROUND",   (0, 1), (0, -1), colors.HexColor("#253347")),
        ("TEXTCOLOR",    (0, 1), (0, -1), C_WHITE),
        ("FONTNAME",     (0, 1), (0, -1), "Helvetica-Bold"),
    ])
    ts_table = Table(ts_data, colWidths=[90, 185, 250])
    ts_table.setStyle(ts_style)
    story.append(ts_table)

    story.append(PageBreak())

    # ── Page 3: API Reference ────────────────────────────────────
    story.append(Paragraph("API Reference", h1))
    story.append(hr())

    story.append(Paragraph("REST Endpoints — Game Management", h2))
    api_data = [
        ["Method", "Route", "Description"],
        ["POST", "/api/game/start", "Start a new game; choose white or black"],
        ["POST", "/api/game/move", "Submit a move (UCI notation); receive AI response"],
        ["GET",  "/api/game/{id}/state", "Retrieve current board state & move history"],
        ["POST", "/api/game/{id}/resign", "Resign the active game"],
    ]
    method_colors = {"POST": C_GREEN, "GET": C_BLUE, "DELETE": C_RED}

    def api_table_style(data):
        s = TableStyle([
            ("BACKGROUND",   (0, 0), (-1, 0),  C_ACCENT),
            ("TEXTCOLOR",    (0, 0), (-1, 0),  C_WHITE),
            ("FONTNAME",     (0, 0), (-1, 0),  "Helvetica-Bold"),
            ("FONTSIZE",     (0, 0), (-1, -1), 8.5),
            ("FONTNAME",     (0, 1), (-1, -1), "Helvetica"),
            ("TEXTCOLOR",    (0, 1), (-1, -1), C_MUTED),
            ("ROWBACKGROUNDS", (0, 1), (-1, -1), [C_PANEL, colors.HexColor("#253347")]),
            ("GRID",         (0, 0), (-1, -1), 0.4, C_BORDER),
            ("LEFTPADDING",  (0, 0), (-1, -1), 8),
            ("TOPPADDING",   (0, 0), (-1, -1), 5),
            ("BOTTOMPADDING",(0, 0), (-1, -1), 5),
            ("FONTNAME",     (0, 1), (0, -1),  "Helvetica-Bold"),
        ])
        for i, row in enumerate(data[1:], start=1):
            col = method_colors.get(row[0], C_MUTED)
            s.add("TEXTCOLOR", (0, i), (0, i), col)
        return s

    t1 = Table(api_data, colWidths=[55, 185, 295])
    t1.setStyle(api_table_style(api_data))
    story.append(t1)
    story.append(sp(10))

    story.append(Paragraph("REST Endpoints — Engine Analysis", h2))
    api_data2 = [
        ["Method", "Route", "Description"],
        ["POST", "/api/engine/move", "Find best move for a FEN (configurable depth / time)"],
        ["POST", "/api/engine/evaluate", "Return static evaluation score (centipawns)"],
        ["GET",  "/api/engine/perft/{depth}", "Perft node-count test at given depth"],
    ]
    t2 = Table(api_data2, colWidths=[55, 185, 295])
    t2.setStyle(api_table_style(api_data2))
    story.append(t2)
    story.append(sp(10))

    story.append(Paragraph("SignalR Hub — /hubs/engine", h2))
    signalr_data = [
        ["Direction", "Event / Method", "Payload"],
        ["Client → Server", "FindBestMove(fen, depth, timeMs)", "Triggers async search"],
        ["Server → Client", "searchProgress", "{ depth, score, nodes, timeMs, pv }"],
        ["Server → Client", "bestMove", "{ move (UCI), score (cp) }"],
        ["Server → Client", "searchError", "{ message }"],
    ]
    sr_style = TableStyle([
        ("BACKGROUND",   (0, 0), (-1, 0),  C_CYAN),
        ("TEXTCOLOR",    (0, 0), (-1, 0),  C_BG),
        ("FONTNAME",     (0, 0), (-1, 0),  "Helvetica-Bold"),
        ("FONTSIZE",     (0, 0), (-1, -1), 8.5),
        ("FONTNAME",     (0, 1), (-1, -1), "Helvetica"),
        ("TEXTCOLOR",    (0, 1), (-1, -1), C_MUTED),
        ("ROWBACKGROUNDS", (0, 1), (-1, -1), [C_PANEL, colors.HexColor("#253347")]),
        ("GRID",         (0, 0), (-1, -1), 0.4, C_BORDER),
        ("LEFTPADDING",  (0, 0), (-1, -1), 8),
        ("TOPPADDING",   (0, 0), (-1, -1), 5),
        ("BOTTOMPADDING",(0, 0), (-1, -1), 5),
    ])
    for i, row in enumerate(signalr_data[1:], start=1):
        if "→ Server" in row[0]:
            sr_style.add("TEXTCOLOR", (0, i), (0, i), C_GREEN)
        else:
            sr_style.add("TEXTCOLOR", (0, i), (0, i), C_ORANGE)
    t3 = Table(signalr_data, colWidths=[110, 195, 230])
    t3.setStyle(sr_style)
    story.append(t3)

    story.append(PageBreak())

    # ── Page 4: Engine Design ────────────────────────────────────
    story.append(Paragraph("Engine Design", h1))
    story.append(hr())

    story.append(Paragraph("Move Encoding", h2))
    story.append(MoveBitDiagram(width=W - 60))
    story.append(sp(8))
    story.append(Paragraph(
        "Each chess move is packed into a single 32-bit integer. This eliminates heap allocations "
        "during move generation and keeps the move list cache-friendly.", body))
    story.append(sp(10))

    # Two-column layout: search flowchart + evaluation
    story.append(Paragraph("Search Algorithm", h2))
    story.append(SearchFlowchart(width=W - 60))
    story.append(sp(8))

    # Search parameters table
    story.append(Paragraph("Search Parameters", h3))
    sp_data = [
        ["Parameter", "Value", "Notes"],
        ["Max depth", "30 plies", "Iterative deepening starts at depth 1"],
        ["Time limit", "60 seconds", "Per move; deepening stops when limit reached"],
        ["Aspiration window", "±50 cp", "Widened on fail-high / fail-low"],
        ["LMR trigger", "depth ≥ 3, move > 4", "Reduces non-capture quiet moves"],
        ["TT size", "8,388,608 entries", "Depth-preferred replacement"],
        ["Killer moves", "2 per ply", "Non-capture moves that caused β cut-offs"],
    ]
    sp_style = TableStyle([
        ("BACKGROUND",   (0, 0), (-1, 0),  C_GREEN),
        ("TEXTCOLOR",    (0, 0), (-1, 0),  C_BG),
        ("FONTNAME",     (0, 0), (-1, 0),  "Helvetica-Bold"),
        ("FONTSIZE",     (0, 0), (-1, -1), 8.5),
        ("FONTNAME",     (0, 1), (-1, -1), "Helvetica"),
        ("TEXTCOLOR",    (0, 1), (-1, -1), C_MUTED),
        ("TEXTCOLOR",    (1, 1), (1, -1),  C_WHITE),
        ("FONTNAME",     (1, 1), (1, -1),  "Helvetica-Bold"),
        ("ROWBACKGROUNDS", (0, 1), (-1, -1), [C_PANEL, colors.HexColor("#253347")]),
        ("GRID",         (0, 0), (-1, -1), 0.4, C_BORDER),
        ("LEFTPADDING",  (0, 0), (-1, -1), 8),
        ("TOPPADDING",   (0, 0), (-1, -1), 5),
        ("BOTTOMPADDING",(0, 0), (-1, -1), 5),
    ])
    sp_table = Table(sp_data, colWidths=[130, 100, 305])
    sp_table.setStyle(sp_style)
    story.append(sp_table)

    story.append(PageBreak())

    # ── Page 5: Evaluation + Data Flow ──────────────────────────
    story.append(Paragraph("Evaluation & Data Flow", h1))
    story.append(hr())

    story.append(Paragraph("Position Evaluation", h2))
    eval_data = [
        ["Term", "Detail", "Impact"],
        ["Material", "P=100  N=320  B=330  R=500  Q=900  K=20 000", "Primary signal"],
        ["Piece-square tables", "Separate middlegame & endgame PSTs per piece", "Positional bonuses"],
        ["Pawn structure", "Isolated / doubled penalties; passed pawn bonus (scales by rank)", "Endgame-weighted"],
        ["King safety", "Count enemy attacks near king; endgame → centralize king", "Critical in MG"],
        ["Tapered eval", "Score = (MG × phase + EG × (24−phase)) / 24", "Smooth MG→EG blend"],
    ]
    ev_style = TableStyle([
        ("BACKGROUND",   (0, 0), (-1, 0),  C_ORANGE),
        ("TEXTCOLOR",    (0, 0), (-1, 0),  C_BG),
        ("FONTNAME",     (0, 0), (-1, 0),  "Helvetica-Bold"),
        ("FONTSIZE",     (0, 0), (-1, -1), 8),
        ("FONTNAME",     (0, 1), (-1, -1), "Helvetica"),
        ("TEXTCOLOR",    (0, 1), (-1, -1), C_MUTED),
        ("TEXTCOLOR",    (0, 1), (0, -1),  C_WHITE),
        ("FONTNAME",     (0, 1), (0, -1),  "Helvetica-Bold"),
        ("ROWBACKGROUNDS", (0, 1), (-1, -1), [C_PANEL, colors.HexColor("#253347")]),
        ("GRID",         (0, 0), (-1, -1), 0.4, C_BORDER),
        ("LEFTPADDING",  (0, 0), (-1, -1), 8),
        ("TOPPADDING",   (0, 0), (-1, -1), 5),
        ("BOTTOMPADDING",(0, 0), (-1, -1), 5),
    ])
    ev_table = Table(eval_data, colWidths=[100, 260, 175])
    ev_table.setStyle(ev_style)
    story.append(ev_table)
    story.append(sp(14))

    # Game data flow
    story.append(Paragraph("Game Data Flow", h2))
    flow_data = [
        ["Step", "Actor", "Action", "Output"],
        ["1", "Player", "Select color, click Start", "POST /api/game/start"],
        ["2", "API", "Init board, store game state", "gameId + starting FEN"],
        ["3", "Player", "Drag piece on board", "POST /api/game/move (UCI)"],
        ["4", "API", "Validate move, apply to board", "Player FEN"],
        ["5", "AI Search", "Iterative deepening α-β", "Best move (UCI)"],
        ["6", "API", "Apply AI move, check game status", "AI FEN + status"],
        ["7", "SignalR", "Stream search progress events", "depth / score / PV"],
        ["8", "Frontend", "Update board, eval bar, PV panel", "Visual update"],
    ]
    fl_style = TableStyle([
        ("BACKGROUND",   (0, 0), (-1, 0),  C_PANEL),
        ("TEXTCOLOR",    (0, 0), (-1, 0),  C_ACCENT),
        ("FONTNAME",     (0, 0), (-1, 0),  "Helvetica-Bold"),
        ("FONTSIZE",     (0, 0), (-1, -1), 8.5),
        ("FONTNAME",     (0, 1), (-1, -1), "Helvetica"),
        ("TEXTCOLOR",    (0, 1), (-1, -1), C_MUTED),
        ("TEXTCOLOR",    (0, 1), (0, -1),  C_ACCENT),
        ("FONTNAME",     (0, 1), (0, -1),  "Helvetica-Bold"),
        ("ROWBACKGROUNDS", (0, 1), (-1, -1), [C_PANEL, colors.HexColor("#253347")]),
        ("GRID",         (0, 0), (-1, -1), 0.4, C_BORDER),
        ("LEFTPADDING",  (0, 0), (-1, -1), 8),
        ("TOPPADDING",   (0, 0), (-1, -1), 5),
        ("BOTTOMPADDING",(0, 0), (-1, -1), 5),
    ])
    actor_colors = {
        "Player": C_BLUE, "API": C_ACCENT,
        "AI Search": C_GREEN, "SignalR": C_CYAN, "Frontend": C_ORANGE,
    }
    for i, row in enumerate(flow_data[1:], start=1):
        col = actor_colors.get(row[1], C_MUTED)
        fl_style.add("TEXTCOLOR", (1, i), (1, i), col)
        fl_style.add("FONTNAME",  (1, i), (1, i), "Helvetica-Bold")
    fl_table = Table(flow_data, colWidths=[28, 75, 190, 242])
    fl_table.setStyle(fl_style)
    story.append(fl_table)
    story.append(sp(14))

    # Info boxes row: Storage + Frontend
    story.append(Paragraph("Key Design Decisions", h2))
    boxes_data = [
        InfoBox("In-Memory Storage", [
            "• ConcurrentDictionary<string, GameState>",
            "• No database — state lost on restart",
            "• TranspositionTable is DI singleton",
            "• Search is DI transient (one per request)",
            "→ Scale-out would require a distributed cache",
        ], C_YELLOW, width=(W - 80) / 2),
        InfoBox("Frontend Architecture", [
            "• chess.js validates moves client-side",
            "• react-chessboard renders the board",
            "• SignalR streams analysis in real time",
            "• Two-screen flow: setup → game",
            "→ Analysis panel updates per depth level",
        ], C_BLUE, width=(W - 80) / 2),
    ]
    boxes_table = Table([[boxes_data[0], boxes_data[1]]], colWidths=[(W - 80) / 2, (W - 80) / 2])
    boxes_table.setStyle(TableStyle([
        ("LEFTPADDING",  (0, 0), (-1, -1), 0),
        ("RIGHTPADDING", (0, 0), (-1, -1), 10),
        ("TOPPADDING",   (0, 0), (-1, -1), 0),
        ("BOTTOMPADDING",(0, 0), (-1, -1), 0),
    ]))
    story.append(boxes_table)

    story.append(PageBreak())

    # ── Page 6: Project Structure ─────────────────────────────
    story.append(Paragraph("Project Structure", h1))
    story.append(hr())

    story.append(Paragraph("Repository Layout", h2))
    dir_data = [
        ["Path", "Purpose"],
        ["Chess.Engine.Core/", "Bitboard engine — board, moves, Zobrist, move gen"],
        ["Chess.Engine.AI/", "Alpha-Beta search, evaluation, transposition table"],
        ["Chess.API/", "ASP.NET Core app — controllers, SignalR hub, DTOs"],
        ["chess-ui/", "React + TypeScript frontend (Vite)"],
        ["Chess.Tests/", "xUnit tests — board, perft, evaluation, search, TT"],
        ["pseudocode/", "Human-readable design documents for each layer"],
        ["README.md", "Project overview, setup guide, API reference"],
    ]
    dir_style = TableStyle([
        ("BACKGROUND",   (0, 0), (-1, 0),  C_ACCENT),
        ("TEXTCOLOR",    (0, 0), (-1, 0),  C_WHITE),
        ("FONTNAME",     (0, 0), (-1, 0),  "Helvetica-Bold"),
        ("FONTSIZE",     (0, 0), (-1, -1), 8.5),
        ("FONTNAME",     (0, 1), (-1, -1), "Helvetica"),
        ("TEXTCOLOR",    (0, 1), (-1, -1), C_MUTED),
        ("TEXTCOLOR",    (0, 1), (0, -1),  C_GREEN),
        ("FONTNAME",     (0, 1), (0, -1),  "Courier-Bold"),
        ("ROWBACKGROUNDS", (0, 1), (-1, -1), [C_PANEL, colors.HexColor("#253347")]),
        ("GRID",         (0, 0), (-1, -1), 0.4, C_BORDER),
        ("LEFTPADDING",  (0, 0), (-1, -1), 8),
        ("TOPPADDING",   (0, 0), (-1, -1), 5),
        ("BOTTOMPADDING",(0, 0), (-1, -1), 5),
    ])
    dir_table = Table(dir_data, colWidths=[175, 360])
    dir_table.setStyle(dir_style)
    story.append(dir_table)
    story.append(sp(14))

    # Key files
    story.append(Paragraph("Key Source Files", h2))
    files_data = [
        ["File", "Layer", "Responsibility"],
        ["Board.cs", "Core", "Board state, FEN parsing, MakeMove / UndoMove"],
        ["Bitboards.cs", "Core", "Magic bitboard attack tables (rook, bishop, queen, knight, king, pawn)"],
        ["MoveGenerator.cs", "Core", "Pseudo-legal generation → legal filter via IsInCheck"],
        ["Zobrist.cs", "Core", "Incremental Zobrist hashing for position identity"],
        ["Search.cs", "AI", "Iterative-deepening α-β with LMR, killers, history, aspiration"],
        ["Evaluation.cs", "AI", "Tapered material + PST + pawn structure + king safety"],
        ["TranspositionTable.cs", "AI", "8.4 M entry hash table with depth-preferred replacement"],
        ["GameController.cs", "API", "POST start / move / resign; in-memory game store"],
        ["EngineController.cs", "API", "POST best-move / evaluate; GET perft"],
        ["EngineHub.cs", "API", "SignalR hub streaming search progress to browser"],
        ["App.tsx", "UI", "Root component — game state, move handling, SignalR wiring"],
        ["engineHub.ts", "UI", "SignalR client wrapper (connect, analyze, events)"],
        ["gameApi.ts", "UI", "REST client helpers (startGame, makeMove, resignGame)"],
    ]
    layer_colors = {"Core": C_ORANGE, "AI": C_GREEN, "API": C_ACCENT, "UI": C_BLUE}
    fi_style = TableStyle([
        ("BACKGROUND",   (0, 0), (-1, 0),  C_PANEL),
        ("TEXTCOLOR",    (0, 0), (-1, 0),  C_WHITE),
        ("FONTNAME",     (0, 0), (-1, 0),  "Helvetica-Bold"),
        ("FONTSIZE",     (0, 0), (-1, -1), 8),
        ("FONTNAME",     (0, 1), (0, -1),  "Courier"),
        ("TEXTCOLOR",    (0, 1), (0, -1),  C_GREEN),
        ("FONTNAME",     (2, 1), (2, -1),  "Helvetica"),
        ("TEXTCOLOR",    (2, 1), (2, -1),  C_MUTED),
        ("ROWBACKGROUNDS", (0, 1), (-1, -1), [C_PANEL, colors.HexColor("#253347")]),
        ("GRID",         (0, 0), (-1, -1), 0.4, C_BORDER),
        ("LEFTPADDING",  (0, 0), (-1, -1), 8),
        ("TOPPADDING",   (0, 0), (-1, -1), 4),
        ("BOTTOMPADDING",(0, 0), (-1, -1), 4),
    ])
    for i, row in enumerate(files_data[1:], start=1):
        col = layer_colors.get(row[1], C_MUTED)
        fi_style.add("TEXTCOLOR", (1, i), (1, i), col)
        fi_style.add("FONTNAME",  (1, i), (1, i), "Helvetica-Bold")
    fi_table = Table(files_data, colWidths=[145, 50, 340])
    fi_table.setStyle(fi_style)
    story.append(fi_table)

    # Build PDF
    doc.build(story,
              onFirstPage=cover_page,
              onLaterPages=normal_page)
    print(f"PDF written to: {out_path}")


if __name__ == "__main__":
    out = os.path.join(os.path.dirname(__file__), "Chess_API_Overview.pdf")
    build_pdf(out)
