"""Genera el JSON optimizado que consume el módulo de indicadores de trámites.

Uso:
  python scripts/procesar_tramites.py --input "C:\ruta\datos_recopilados_limpio.xlsx"
"""
from __future__ import annotations

import argparse
import json
from datetime import date
from pathlib import Path

import pandas as pd


# Solo se incluye el nombre cuando puede confirmarse con certeza. Los demás
# códigos se muestran tal como llegan de SOL y quedan listos para completar.
INSTITUCIONES_CONFIRMADAS = {
    "SOL_HN_CONSUCOOP": {
        "sigla": "CONSUCOOP",
        "nombre": "Consejo Nacional Supervisor de Cooperativas",
    },
}


def valor_numero(value):
    return None if pd.isna(value) else float(value)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--input", required=True, help="Archivo XLSX fuente")
    parser.add_argument(
        "--output",
        default="src/Web/App_Data/tramites-indicadores.json",
        help="JSON interno generado; se expone únicamente mediante la página autenticada",
    )
    args = parser.parse_args()

    frame = pd.read_excel(args.input, sheet_name="Datos")
    frame.columns = [str(col).replace("A�o", "Año").strip() for col in frame.columns]
    required = {
        "Base de datos", "codigo_tipo_tramite", "nombre", "fecha_solicitud",
        "Año", "Mes", "fecha_inicio", "fecha_fin_esperada", "fecha_fin",
        "diferencia dias esperada", "diferencia dias real", "desfase",
    }
    missing = required.difference(frame.columns)
    if missing:
        raise ValueError(f"Columnas requeridas ausentes: {', '.join(sorted(missing))}")

    for column in ["fecha_solicitud", "fecha_inicio", "fecha_fin_esperada", "fecha_fin"]:
        frame[column] = pd.to_datetime(frame[column], errors="coerce")

    reference = frame["fecha_solicitud"].max()
    if pd.isna(reference):
        reference = pd.Timestamp(date.today())

    institutions = []
    for code in sorted(frame["Base de datos"].dropna().astype(str).unique()):
        configured = INSTITUCIONES_CONFIRMADAS.get(code, {})
        institutions.append({
            "codigo": code,
            "sigla": configured.get("sigla", code),
            "nombre": configured.get("nombre"),
            "pendienteConfirmacion": code not in INSTITUCIONES_CONFIRMADAS,
        })

    records = []
    for _, row in frame.iterrows():
        finished = pd.notna(row["fecha_fin"])
        started = pd.notna(row["fecha_inicio"])
        expected_date = row["fecha_fin_esperada"]
        overdue = not finished and pd.notna(expected_date) and expected_date < reference
        if finished:
            status = "finalizada"
        elif overdue:
            status = "vencida"
        elif not started:
            status = "sin_iniciar"
        elif pd.notna(expected_date):
            status = "en_proceso"
        else:
            status = "pendiente"

        offset = valor_numero(row["desfase"])
        compliance = "sin_informacion"
        if finished and offset is not None:
            compliance = "dentro_plazo" if offset <= 0 else "fuera_plazo"

        records.append({
            "institucion": str(row["Base de datos"]),
            "tramiteCodigo": str(row["codigo_tipo_tramite"]),
            "tramite": None if pd.isna(row["nombre"]) else str(row["nombre"]),
            "anio": None if pd.isna(row["Año"]) else int(row["Año"]),
            "mes": None if pd.isna(row["Mes"]) else int(row["Mes"]),
            "estado": status,
            "cumplimiento": compliance,
            "diasEstimados": valor_numero(row["diferencia dias esperada"]),
            "diasReales": valor_numero(row["diferencia dias real"]) if finished else None,
            "desfase": offset,
        })

    output = {
        "metadata": {
            "actualizado": reference.date().isoformat(),
            "fechaReferencia": reference.date().isoformat(),
            "registros": len(records),
            "instituciones": len(institutions),
            "tramitesUnicos": len(frame[["Base de datos", "codigo_tipo_tramite"]].drop_duplicates()),
            "fuente": Path(args.input).name,
        },
        "instituciones": institutions,
        "registros": records,
    }

    destination = Path(args.output)
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_text(json.dumps(output, ensure_ascii=False, separators=(",", ":")), encoding="utf-8")
    print(f"Generado {destination}: {len(records):,} registros, {len(institutions)} instituciones")


if __name__ == "__main__":
    main()
