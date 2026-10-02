#!/usr/bin/env python3
"""Guardian del Manual de Marca DIGER.

Comprueba que el CSS del portal respete el Manual de Marca:

  1. No queda rastro del dorado (no esta en el manual).
  2. Todo color de marca o derivado cae a <=8 grados de tono de un color
     del manual. Lo que se pase es un tono ajeno.
  3. Cada par texto/fondo declarado alcanza WCAG AA (4.5:1).
  4. Censo de literales hex, que debe bajar entre etapas.

Uso:
    python scripts/verificar_marca.py            # informe + codigo de salida
    python scripts/verificar_marca.py --censo    # solo el censo de literales

Codigo de salida 0 si todo pasa, 1 si algo falla.
"""
from __future__ import annotations

import argparse
import colorsys
import re
import sys
from pathlib import Path

RAIZ = Path(__file__).resolve().parent.parent
CSS = RAIZ / "src" / "Web" / "wwwroot" / "css"
VISTAS = RAIZ / "src" / "Web" / "Pages"

# -- Manual de Marca DIGER, pag. 09 -------------------------------------
MANUAL = {
    "azul oscuro": "#02395e",
    "azul medio": "#1a65a3",
    "celeste": "#7acbdd",
}

TOLERANCIA_TONO = 8  # grados

# Prohibido: el dorado nunca estuvo en el manual.
PROHIBIDOS = {
    "#ad8411": "dorado institucional (no existe en el Manual de Marca)",
    "#c79621": "dorado claro (no existe en el Manual de Marca)",
    "#0a2d6e": "azul violaceo, tono ajeno por 12 grados",
    "#1d4ed8": "indigo, tono ajeno por 17 grados",
    "#1455a4": "azul que reclamaba ser el corporativo sin serlo",
    "#0c447c": "azul sin anclar al hex del manual",
}

# Pares texto/fondo que deben alcanzar AA. (texto, fondo, descripcion)
PARES_AA = [
    ("#02395e", "#ffffff", "azul oscuro sobre blanco"),
    ("#02395e", "#eef1f7", "azul oscuro sobre fondo"),
    ("#1a65a3", "#ffffff", "azul medio sobre blanco"),
    ("#1a65a3", "#eef1f7", "azul medio sobre fondo"),
    ("#ffffff", "#02395e", "blanco sobre header navy"),
    ("#7acbdd", "#02395e", "celeste sobre header navy"),
    ("#02395e", "#7acbdd", "navy sobre avatar celeste"),
    ("#b45309", "#ffffff", "estado privado sobre blanco"),
]


def rgb(hex_: str) -> tuple[float, float, float]:
    h = hex_.lstrip("#")
    if len(h) == 3:
        h = "".join(c * 2 for c in h)
    return tuple(int(h[i:i + 2], 16) / 255 for i in (0, 2, 4))


def luminancia(hex_: str) -> float:
    canales = [c / 12.92 if c <= 0.03928 else ((c + 0.055) / 1.055) ** 2.4
               for c in rgb(hex_)]
    return 0.2126 * canales[0] + 0.7152 * canales[1] + 0.0722 * canales[2]


def contraste(a: str, b: str) -> float:
    la, lb = luminancia(a), luminancia(b)
    return (max(la, lb) + 0.05) / (min(la, lb) + 0.05)


def tono(hex_: str) -> float:
    r, g, b = rgb(hex_)
    return colorsys.rgb_to_hls(r, g, b)[0] * 360


def distancia_tono(hex_: str) -> tuple[float, str]:
    """Grados hasta el color del manual mas cercano, y cual es."""
    t = tono(hex_)
    mejor, nombre = 360.0, ""
    for n, h in MANUAL.items():
        d = abs(t - tono(h))
        d = min(d, 360 - d)
        if d < mejor:
            mejor, nombre = d, n
    return mejor, nombre


def archivos_css() -> list[Path]:
    return sorted(CSS.glob("*.css"))


def archivos_vista() -> list[Path]:
    return sorted(VISTAS.rglob("*.cshtml"))


def literales(texto: str) -> list[str]:
    return [m.lower() for m in re.findall(r"#[0-9a-fA-F]{6}\b", texto)]


def censo() -> tuple[int, int]:
    en_css = sum(len(literales(f.read_text(encoding="utf-8", errors="replace")))
                 for f in archivos_css())
    en_vistas = sum(len(literales(f.read_text(encoding="utf-8", errors="replace")))
                    for f in archivos_vista())
    return en_css, en_vistas


def main() -> int:
    ap = argparse.ArgumentParser(description="Guardian del Manual de Marca DIGER")
    ap.add_argument("--censo", action="store_true",
                    help="solo el censo de literales hex")
    args = ap.parse_args()

    css_n, vistas_n = censo()
    if args.censo:
        print(f"literales hex -> css: {css_n}  vistas: {vistas_n}  total: {css_n + vistas_n}")
        return 0

    fallos: list[str] = []

    # -- 1. Colores prohibidos ------------------------------------------
    print("[1] Colores prohibidos")
    for archivo in archivos_css() + archivos_vista():
        texto = archivo.read_text(encoding="utf-8", errors="replace").lower()
        for hex_, motivo in PROHIBIDOS.items():
            if hex_ in texto:
                rel = archivo.relative_to(RAIZ)
                fallos.append(f"{rel}: {hex_} - {motivo}")
    for nombre in ("--diger-gold", "--diger-gold-light"):
        for archivo in archivos_css() + archivos_vista():
            if nombre in archivo.read_text(encoding="utf-8", errors="replace"):
                fallos.append(f"{archivo.relative_to(RAIZ)}: token {nombre} sigue vivo")
    print("    sin hallazgos" if not fallos else f"    {len(fallos)} hallazgo(s)")

    # -- 2. Pertenencia de tono -----------------------------------------
    print("[2] Pertenencia de tono (<=%d grados)" % TOLERANCIA_TONO)
    tokens_css = (CSS / "tokens.css").read_text(encoding="utf-8", errors="replace")
    declarados = re.findall(r"(--diger-(?:blue|celeste|cyan)[\w-]*)\s*:\s*(#[0-9a-fA-F]{3,6})",
                            tokens_css)
    vistos: set[str] = set()
    for nombre, valor in declarados:
        if valor.lower() in vistos:
            continue
        vistos.add(valor.lower())
        d, cerca = distancia_tono(valor)
        # Los tintes muy claros (pills, bordes) pierden tono por saturacion baja:
        # se exceptuan por luminancia, no por nombre.
        if luminancia(valor) > 0.75:
            print(f"    {nombre:24s} {valor}  tinte claro, exento")
            continue
        if d > TOLERANCIA_TONO:
            fallos.append(f"tokens.css: {nombre} = {valor} esta a {d:.0f} grados de {cerca}")
            print(f"    {nombre:24s} {valor}  {d:5.1f} grados  FALLA")
        else:
            print(f"    {nombre:24s} {valor}  {d:5.1f} grados  ok ({cerca})")

    # -- 3. Contraste AA ------------------------------------------------
    print("[3] Contraste WCAG AA (4.5:1)")
    for fg, bg, desc in PARES_AA:
        r = contraste(fg, bg)
        estado = "ok" if r >= 4.5 else "FALLA"
        if r < 4.5:
            fallos.append(f"contraste: {desc} = {r:.2f}, por debajo de AA")
        print(f"    {desc:34s} {r:5.2f}  {estado}")

    # -- 4. Censo -------------------------------------------------------
    print("[4] Censo de literales hex")
    print(f"    css: {css_n}   vistas: {vistas_n}   total: {css_n + vistas_n}")

    print()
    if fallos:
        print(f"RESULTADO: {len(fallos)} fallo(s)")
        for f in fallos[:40]:
            print(f"  - {f}")
        if len(fallos) > 40:
            print(f"  ... y {len(fallos) - 40} mas")
        return 1
    print("RESULTADO: el portal respeta el Manual de Marca")
    return 0


if __name__ == "__main__":
    sys.exit(main())
