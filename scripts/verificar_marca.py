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

# Las cuatro superficies que la auditoria inicial no cubria. Pintan marca
# igual que el CSS, pero ningun token CSS las alcanza: aqui el color viaja
# en una cadena de JavaScript o en una constante de C#. Mientras el guardian
# no las recorrio, el verde era real pero acotado — y se leia como si el
# plan hubiera terminado.
JS = RAIZ / "src" / "Web" / "wwwroot" / "js"
MODELOS = RAIZ / "src" / "Web" / "Models"
REPORTES = RAIZ / "src" / "Infrastructure" / "Reports"

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
    # Pares que salen de C#, no del CSS: CalendarioUi.cs y CompromisoUi.cs
    # devuelven (fondo, texto) y ningun token CSS los alcanza. Van aqui para
    # que un cambio de valor no pueda bajar de AA sin que nadie se entere.
    ("#02395e", "#e6f1fb", "calendario: reunion"),
    ("#712b13", "#faece7", "calendario: ticket creado"),
    ("#085041", "#e1f5ee", "calendario: respuesta a ticket"),
    ("#3c3489", "#eeedfe", "calendario: cambio de estado"),
    ("#444441", "#f1efe8", "calendario: asignacion"),
    ("#633806", "#faeeda", "calendario: expediente"),
    ("#02395e", "#e6f1fb", "compromiso: pendiente"),
    ("#854f0b", "#faeeda", "compromiso: en progreso"),
    ("#6b21a8", "#f3e8ff", "compromiso: en revision"),
    ("#27500a", "#eaf3de", "compromiso: cumplido"),
    ("#5b21b6", "#ede9fe", "compromiso: reprogramado"),
    ("#5f5e5a", "#f1efe8", "compromiso: otro"),
    ("#a32d2d", "#fcebeb", "compromiso: vencido"),
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


def archivos_codigo() -> list[Path]:
    """JavaScript y C# que pintan color. Se excluyen los .ttf embarcados."""
    return (sorted(JS.glob("*.js"))
            + sorted(MODELOS.glob("*Ui.cs"))
            + sorted(REPORTES.glob("*.cs")))


def literales(texto: str) -> list[str]:
    return [m.lower() for m in re.findall(r"#[0-9a-fA-F]{6}\b", texto)]


# El mismo color prohibido escrito como rgb()/rgba() no lleva almohadilla y
# se escapaba del censo y de la comprobacion [1]: rgba(10, 45, 110, .12) es
# #0a2d6e en decimal. Habia 39 asi, de modo que el guardian podia dar verde
# con el color prohibido vivo. Se comprueban las dos escrituras.
def _patron_rgb(hex_: str) -> re.Pattern:
    h = hex_.lstrip("#")
    r, g, b = int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16)
    return re.compile(rf"rgba?\(\s*{r}\s*,\s*{g}\s*,\s*{b}\s*[,)]")


PATRONES_RGB = {hex_: _patron_rgb(hex_) for hex_ in PROHIBIDOS}


def funciones_rgb(texto: str) -> list[str]:
    """Usos de un color PROHIBIDO escritos como rgb()/rgba()."""
    hallados = []
    for hex_, patron in PATRONES_RGB.items():
        hallados += [hex_] * len(patron.findall(texto))
    return hallados


def censo() -> tuple[int, int, int]:
    en_css = sum(len(literales(f.read_text(encoding="utf-8", errors="replace")))
                 for f in archivos_css())
    en_vistas = sum(len(literales(f.read_text(encoding="utf-8", errors="replace")))
                    for f in archivos_vista())
    en_codigo = sum(len(literales(f.read_text(encoding="utf-8", errors="replace")))
                    for f in archivos_codigo())
    return en_css, en_vistas, en_codigo


def censo_rgb() -> tuple[int, int]:
    en_css = sum(len(funciones_rgb(f.read_text(encoding="utf-8", errors="replace").lower()))
                 for f in archivos_css())
    # Vistas y codigo van juntos: en ambos el color viaja en una cadena y la
    # distincion no aporta nada al leer el informe.
    en_resto = sum(len(funciones_rgb(f.read_text(encoding="utf-8", errors="replace").lower()))
                   for f in archivos_vista() + archivos_codigo())
    return en_css, en_resto


def main() -> int:
    ap = argparse.ArgumentParser(description="Guardian del Manual de Marca DIGER")
    ap.add_argument("--censo", action="store_true",
                    help="solo el censo de literales hex")
    args = ap.parse_args()

    css_n, vistas_n, codigo_n = censo()
    if args.censo:
        print(f"literales hex -> css: {css_n}  vistas: {vistas_n}  "
              f"codigo: {codigo_n}  total: {css_n + vistas_n + codigo_n}")
        return 0

    fallos: list[str] = []

    # -- 1. Colores prohibidos ------------------------------------------
    print("[1] Colores prohibidos")
    for archivo in archivos_css() + archivos_vista() + archivos_codigo():
        texto = archivo.read_text(encoding="utf-8", errors="replace").lower()
        rel = archivo.relative_to(RAIZ)
        for hex_, motivo in PROHIBIDOS.items():
            if hex_ in texto:
                fallos.append(f"{rel}: {hex_} - {motivo}")
            n = len(PATRONES_RGB[hex_].findall(texto))
            if n:
                fallos.append(f"{rel}: {hex_} escrito como rgb/rgba, {n} vez(ces) - {motivo}")
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
    # El censo hex se deja intacto: tiene linea base guardada y la metrica de
    # avance del plan es que baje monotonamente. Los rgb/rgba van en su propia
    # cuenta para no mover ese numero bajo los pies de nadie.
    print("[4] Censo de literales hex")
    print(f"    css: {css_n}   vistas: {vistas_n}   codigo: {codigo_n}"
          f"   total: {css_n + vistas_n + codigo_n}")
    rgb_css, rgb_resto = censo_rgb()
    print(f"    prohibidos como rgb/rgba -> css: {rgb_css}   vistas y codigo: {rgb_resto}"
          f"   total: {rgb_css + rgb_resto}")

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
