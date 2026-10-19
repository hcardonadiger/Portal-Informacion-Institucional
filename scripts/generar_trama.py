#!/usr/bin/env python3
"""
Genera la trama de fondo del portal: una red de nodos, en dos temas.

    python scripts/generar_trama.py

Escribe dos SVG en src/Web/wwwroot/img/:

    trama-nodos-oscuro.svg   celeste sobre el suelo navy
    trama-nodos-claro.svg    navy sobre el suelo claro

Es determinista (semilla fija): volver a correrlo no ensucia el diff si no se
cambio un parametro.


QUE DIBUJA
----------
Puntos repartidos por el lienzo, unidos con sus vecinos cercanos. Dice
interoperabilidad: instituciones que se conectan.

El centro queda DESPEJADO a proposito. La probabilidad de aceptar un punto
crece con la distancia al centro, de modo que el patron vive en los margenes
y donde va el dato queda aire. Eso es lo que permite que una tabla densa se
lea encima sin que el fondo compita con ella.


LOS DOS PARAMETROS QUE IMPORTAN, Y POR QUE VAN JUNTOS
-----------------------------------------------------
AREA_POR_PUNTO fija cuantos nodos hay; ALCANCE, a que distancia se unen dos.
Parecen independientes y NO lo son: lo que el ojo lee como "red" es el numero
de VECINOS que tiene cada nodo, y ese numero sale de los dos a la vez:

    vecinos = pi * ALCANCE^2 / AREA_POR_PUNTO

Subir la densidad sin tocar el alcance dispara los vecinos y la red se
convierte en maraña: al duplicar los nodos con ALCANCE fijo, los vecinos
pasan de 6,1 a 11,6 y el dibujo se empasta. Por eso ALCANCE no se escribe a
mano: se DERIVA de la densidad para mantener VECINOS constante. Cambie
AREA_POR_PUNTO y el alcance se acomoda solo.

VECINOS = 6 es lo que hace que esto se lea como una red de enlaces y no como
un tejido. Por debajo de 4 se deshilacha en grupitos sueltos; por encima de 8
empieza a empastarse.


POR QUE DOS ARCHIVOS Y NO UNO
-----------------------------
Un SVG servido como `background-image` no resuelve `var(--diger-*)`: los
tokens del documento no llegan a el. Asi que el color va horneado y hace
falta un archivo por tema. La paleta trae la simetria dentro: el celeste da
10,47:1 contra el suelo oscuro y el navy 10,61:1 contra el claro --dos
centesimas de diferencia-- asi que la misma curva de alfa sirve en los dos
con solo recortarla por RECORTE_CLARO, porque la tinta oscura sobre suelo
claro pesa mas a la vista que la clara sobre oscuro.

Los tres colores son los del Manual de Marca DIGER, pag. 09. No hay ninguno
nuevo: el guardian (scripts/verificar_marca.py) sigue pasando.

La CSP del portal declara `img-src 'self' data:`, asi que un SVG servido
desde wwwroot entra sin tocar la politica.


COMO SE COMPRUEBA QUE QUEDO BIEN
--------------------------------
Rasterizando y midiendo, no a ojo:

    chrome --headless=new --disable-gpu --window-size=1600,1000 \\
        --screenshot=par.png http://127.0.0.1:8765/pagina-con-el-svg-de-fondo

y luego midiendo el desvio medio respecto al suelo por franjas verticales: el
centro tiene que dar menos que los bordes. Si el centro se acerca a los
bordes, SESGO_CENTRO se quedo corto y la tabla va a pelear con el fondo.
"""

import math
import random
from pathlib import Path

RAIZ = Path(__file__).resolve().parent.parent
DESTINO = RAIZ / "src" / "Web" / "wwwroot" / "img"

ANCHO, ALTO = 1600, 1000
SEMILLA = 20261014

# -- densidad ---------------------------------------------------------
# Subido de 6500 a 3400 el 2026-10-19: 246 nodos -> 471, casi el doble.
AREA_POR_PUNTO = 3400          # px^2 por nodo
VECINOS = 6.06                 # vecinos medios; define el caracter de la red
SESGO_CENTRO = 1.6             # mayor = centro mas limpio

# Derivado, NO a mano: despejado de vecinos = pi*ALCANCE^2/AREA_POR_PUNTO.
ALCANCE = math.sqrt(VECINOS * AREA_POR_PUNTO / math.pi)

# -- tinta ------------------------------------------------------------
# Manual de Marca DIGER, pag. 09. Ninguno se deriva ni se ajusta.
CELESTE = "#7acbdd"
NAVY = "#02395e"
SUELO_OSCURO = "#050f1a"
SUELO_CLARO = "#eef1f7"

RECORTE_CLARO = 0.85           # la tinta oscura pesa mas a la vista

# Al subir la densidad hay mas trazo por pulgada, asi que con el alfa de
# antes el conjunto se oscurece. Se compensa a la baja para que el fondo
# pese lo mismo que pesaba: mas nodos, no mas tinta. El numero sale de
# rasterizar y comparar el desvio medio contra la version de 246 nodos.
COMPENSA_DENSIDAD = 0.74

MIN_OPACIDAD = 0.012           # por debajo de esto no se ve y solo pesa

# Cuantas opacidades distintas se permiten. Cada segmento tiene la suya, y
# escribirla como atributo propio cuesta mas que el segmento: un <line> con
# su stroke-opacity son ~75 bytes contra los ~22 de un "M x y L x y" dentro
# de un <path> compartido. Redondeando la opacidad a dos decimales quedan
# ~30 niveles, los segmentos se agrupan por nivel y el archivo baja a menos
# de la mitad. La diferencia visual maxima es de 0,005 de alfa sobre valores
# que van de 0,01 a 0,31: no se ve.
DECIMALES_OPACIDAD = 2


# ══ geometria ════════════════════════════════════════════════════════

def puntos():
    """Los nodos, con el centro despejado."""
    cuantos = round(ANCHO * ALTO / AREA_POR_PUNTO)
    rnd = random.Random(SEMILLA)
    pts, intentos = [], 0
    while len(pts) < cuantos and intentos < cuantos * 40:
        intentos += 1
        x, y = rnd.uniform(0, ANCHO), rnd.uniform(0, ALTO)
        dx, dy = (x - ANCHO / 2) / (ANCHO / 2), (y - ALTO / 2) / (ALTO / 2)
        d = min(1.0, math.hypot(dx, dy))
        if rnd.random() > d ** SESGO_CENTRO:
            continue
        # El radio baja con la densidad: 471 puntos del tamaño de 246 se
        # leen como grano grueso. sqrt mantiene constante la tinta total.
        r = (0.9 + rnd.uniform(0, 1.1)) * math.sqrt(AREA_POR_PUNTO / 6500)
        pts.append((x, y, r, d))
    return pts


def _nivel(o):
    """La opacidad, redondeada al nivel que se comparte."""
    return round(o, DECIMALES_OPACIDAD)


# ══ dibujo ═══════════════════════════════════════════════════════════

def capas(pts, mul):
    """Las aristas y los nodos, agrupados por nivel de opacidad.

    Un <path> por nivel con todos sus segmentos dentro, y un <g> por nivel
    para los circulos: ver la nota de DECIMALES_OPACIDAD.
    """
    lineas, circulos = {}, {}
    for i in range(len(pts)):
        x1, y1, _, d1 = pts[i]
        for j in range(i + 1, len(pts)):
            x2, y2, _, d2 = pts[j]
            dist = math.hypot(x1 - x2, y1 - y2)
            if dist >= ALCANCE:
                continue
            o = _nivel((1 - dist / ALCANCE) * 0.30 * ((d1 + d2) / 2) * mul)
            if o < MIN_OPACIDAD:
                continue
            lineas.setdefault(o, []).append(((x1, y1), (x2, y2)))
    for x, y, r, d in pts:
        o = _nivel(0.55 * d * mul)
        if o < MIN_OPACIDAD:
            continue
        circulos.setdefault(o, []).append((x, y, round(r, 1)))

    partes = ['  <g fill="none" stroke-width="1">']
    for o in sorted(lineas):
        d = "".join(f"M{p[0]:.1f} {p[1]:.1f}L{q[0]:.1f} {q[1]:.1f}"
                    for p, q in lineas[o])
        partes.append(f'    <path stroke-opacity="{o:g}" d="{d}"/>')
    partes.append("  </g>")
    partes.append('  <g stroke="none">')
    for o in sorted(circulos):
        cs = "".join(f'<circle cx="{x:.1f}" cy="{y:.1f}" r="{r:g}"/>'
                     for x, y, r in circulos[o])
        partes.append(f'    <g fill-opacity="{o:g}">{cs}</g>')
    partes.append("  </g>")
    return partes


def svg(pts, tinta, suelo, mul, titulo):
    partes = [
        f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {ANCHO} {ALTO}" '
        f'width="{ANCHO}" height="{ALTO}" preserveAspectRatio="xMidYMid slice" '
        f'role="presentation">',
        f"  <!-- {titulo} -->",
        "  <!-- Generado por scripts/generar_trama.py. No editar a mano: "
        "volver a correr el script. -->",
        f'  <rect width="{ANCHO}" height="{ALTO}" fill="{suelo}"/>',
        f'  <g fill="{tinta}" stroke="{tinta}">',
    ]
    partes += capas(pts, mul)
    partes.append("  </g>")
    partes.append("</svg>")
    return "\n".join(partes) + "\n"


# ══ salida ═══════════════════════════════════════════════════════════

ARCHIVOS = [
    ("trama-nodos-oscuro.svg", CELESTE, SUELO_OSCURO, 1.0,
     "Red de nodos, celeste sobre suelo oscuro"),
    ("trama-nodos-claro.svg", NAVY, SUELO_CLARO, RECORTE_CLARO,
     "Red de nodos, navy sobre suelo claro"),
]


def main():
    pts = puntos()
    DESTINO.mkdir(parents=True, exist_ok=True)

    print(f"{len(pts)} nodos  (area {AREA_POR_PUNTO} px2 por punto)")
    print(f"alcance {ALCANCE:.1f} px, derivado para {VECINOS:.2f} vecinos por nodo")
    print()

    for nombre, tinta, suelo, mul, titulo in ARCHIVOS:
        texto = svg(pts, tinta, suelo, mul * COMPENSA_DENSIDAD, titulo)
        destino = DESTINO / nombre
        destino.write_text(texto, encoding="utf-8")
        print(f"  {nombre:26s} {destino.stat().st_size / 1024:6.1f} KB   "
              f"{texto.count('<path'):3d} caminos, {texto.count('<circle'):4d} nodos")

    print()
    print("Referencias en src/Web/wwwroot/css/diger.css "
          "(body.tema-oscuro / body.tema-claro).")


if __name__ == "__main__":
    main()
