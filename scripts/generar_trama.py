#!/usr/bin/env python3
"""
Genera la trama de nodos del fondo oscuro de Proyectos.

Por que un script y no un SVG a mano: son ~200 nodos y ~300 aristas cuyas
coordenadas no significan nada por separado. Escritas a mano serian numeros
muertos que nadie podria ajustar; aqui se ajusta el PARAMETRO (densidad,
alcance, cuanto claro queda al centro) y se vuelve a generar.

Por que un archivo y no canvas en vivo: la CSP del portal declara
`img-src 'self' data:`, asi que un SVG servido desde wwwroot entra sin
tocar la politica. Ademas no cuesta JS en cada carga.

El claro al centro es lo que hace que esta trama sirva bajo una tabla: la
probabilidad de aceptar un nodo crece con la distancia al centro, de modo
que el patron vive en los margenes y donde va el dato queda aire.

    python scripts/generar_trama.py

Escribe src/Web/wwwroot/img/trama-proyectos.svg. Es determinista (semilla
fija): volver a correrlo no ensucia el diff si no se cambio un parametro.
"""

import math
import random
from pathlib import Path

RAIZ = Path(__file__).resolve().parent.parent
SALIDA = RAIZ / "src" / "Web" / "wwwroot" / "img" / "trama-proyectos.svg"

ANCHO, ALTO = 1600, 1000
SEMILLA = 20261013

NODOS = 190          # cuantos se intentan colocar
ALCANCE = 128        # distancia maxima para unir dos nodos con una arista
SESGO_CENTRO = 1.6   # mayor = centro mas limpio
CELESTE = "#7acbdd"  # capa marca, Manual pag. 09


def nodos():
    """Puntos con el centro despejado."""
    rnd = random.Random(SEMILLA)
    pts, intentos = [], 0
    while len(pts) < NODOS and intentos < NODOS * 40:
        intentos += 1
        x, y = rnd.uniform(0, ANCHO), rnd.uniform(0, ALTO)
        # distancia normalizada al centro, 0 en el centro y 1 en las esquinas
        dx, dy = (x - ANCHO / 2) / (ANCHO / 2), (y - ALTO / 2) / (ALTO / 2)
        d = min(1.0, math.hypot(dx, dy))
        if rnd.random() > d ** SESGO_CENTRO:
            continue
        pts.append((x, y, rnd.uniform(1.0, 2.4), d))
    return pts


def svg(pts):
    partes = [
        f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {ANCHO} {ALTO}" '
        f'width="{ANCHO}" height="{ALTO}" preserveAspectRatio="xMidYMid slice" '
        f'role="presentation">',
        "  <!-- Generado por scripts/generar_trama.py. No editar a mano: "
        "volver a correr el script. -->",
        f'  <rect width="{ANCHO}" height="{ALTO}" fill="#050f1a"/>',
        f'  <g stroke="{CELESTE}" fill="none" stroke-width="1">',
    ]

    for i in range(len(pts)):
        for j in range(i + 1, len(pts)):
            x1, y1, _, d1 = pts[i]
            x2, y2, _, d2 = pts[j]
            dist = math.hypot(x1 - x2, y1 - y2)
            if dist >= ALCANCE:
                continue
            # se desvanece con la distancia, y mas aun cerca del centro
            o = (1 - dist / ALCANCE) * 0.30 * ((d1 + d2) / 2)
            if o < 0.012:
                continue
            partes.append(
                f'    <line x1="{x1:.1f}" y1="{y1:.1f}" x2="{x2:.1f}" y2="{y2:.1f}" '
                f'stroke-opacity="{o:.3f}"/>'
            )

    partes.append("  </g>")
    partes.append(f'  <g fill="{CELESTE}">')
    for x, y, r, d in pts:
        partes.append(
            f'    <circle cx="{x:.1f}" cy="{y:.1f}" r="{r:.2f}" '
            f'fill-opacity="{0.55 * d:.3f}"/>'
        )
    partes.append("  </g>")
    partes.append("</svg>")
    return "\n".join(partes) + "\n"


def main():
    pts = nodos()
    SALIDA.parent.mkdir(parents=True, exist_ok=True)
    SALIDA.write_text(svg(pts), encoding="utf-8")
    aristas = sum(1 for linea in svg(pts).splitlines() if "<line" in linea)
    print(f"{SALIDA.relative_to(RAIZ)}")
    print(f"  {len(pts)} nodos, {aristas} aristas, {SALIDA.stat().st_size / 1024:.1f} KB")


if __name__ == "__main__":
    main()
