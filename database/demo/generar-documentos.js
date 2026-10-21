// Genera PDFs mínimos pero válidos (xref con desplazamientos correctos) para el proyecto
// demostrativo. Se usan como documentos del repositorio: tienen que abrirse de verdad.
const fs = require('fs');
const crypto = require('crypto');

function pdf(titulo, lineas) {
  const esc = s => s.replace(/([\\()])/g, '\\$1');
  let texto = 'BT\n/F1 16 Tf\n60 780 Td\n(' + esc(titulo) + ') Tj\n/F1 11 Tf\n';
  lineas.forEach((l, i) => {
    texto += `0 ${i === 0 ? -34 : -18} Td\n(${esc(l)}) Tj\n`;
  });
  texto += 'ET\n';

  const objs = [
    '<</Type/Catalog/Pages 2 0 R>>',
    '<</Type/Pages/Kids[3 0 R]/Count 1>>',
    '<</Type/Page/Parent 2 0 R/MediaBox[0 0 595 842]/Contents 4 0 R/Resources<</Font<</F1 5 0 R>>>>>>',
    `<</Length ${Buffer.byteLength(texto)}>>\nstream\n${texto}endstream`,
    '<</Type/Font/Subtype/Type1/BaseFont/Helvetica/Encoding/WinAnsiEncoding>>'
  ];

  let out = '%PDF-1.4\n';
  const offsets = [];
  objs.forEach((o, i) => {
    offsets.push(Buffer.byteLength(out));
    out += `${i + 1} 0 obj\n${o}\nendobj\n`;
  });

  const xref = Buffer.byteLength(out);
  out += `xref\n0 ${objs.length + 1}\n0000000000 65535 f \n`;
  offsets.forEach(o => { out += String(o).padStart(10, '0') + ' 00000 n \n'; });
  out += `trailer\n<</Size ${objs.length + 1}/Root 1 0 R>>\nstartxref\n${xref}\n%%EOF\n`;

  return Buffer.from(out, 'latin1');
}

const docs = [
  ['acta-diagnostico-v1.pdf', 'Acta de diagnostico de tramites', [
    'Institucion Modelo - Incorporacion a SOL',
    'Fecha: 22 de mayo de 2026',
    '',
    'Se inventariaron 14 tramites y se priorizaron 6 para la primera fase.',
    'La institucion designa contraparte tecnica y confirma disponibilidad',
    'de personal para la etapa de configuracion.',
    '',
    'DOCUMENTO DEMOSTRATIVO - no corresponde a un tramite real.']],

  ['acta-diagnostico-v2.pdf', 'Acta de diagnostico de tramites (corregida)', [
    'Institucion Modelo - Incorporacion a SOL',
    'Fecha: 22 de mayo de 2026 - corregida el 29 de mayo',
    '',
    'Se inventariaron 14 tramites y se priorizaron 6 para la primera fase.',
    'CORRECCION: el tramite 4 se reemplaza por el 9 a solicitud de la',
    'institucion, por mayor volumen de demanda ciudadana.',
    '',
    'DOCUMENTO DEMOSTRATIVO - no corresponde a un tramite real.']],

  ['fichas-tecnicas.pdf', 'Fichas tecnicas de los 6 tramites', [
    'Requisitos, plazos, dependencias y responsables por tramite.',
    'Elaboradas sobre la plantilla institucional vigente.',
    '',
    'DOCUMENTO DEMOSTRATIVO - no corresponde a un tramite real.']],

  ['material-capacitacion.pdf', 'Material de capacitacion a operadores', [
    'Guia del operador, casos de practica y evaluacion.',
    'Dirigido a las 12 personas que atenderan los tramites en SOL.',
    '',
    'DOCUMENTO DEMOSTRATIVO - no corresponde a un tramite real.']]
];

const destino = process.argv[2];
fs.mkdirSync(destino, { recursive: true });

const salida = docs.map(([nombre, titulo, lineas]) => {
  const buf = pdf(titulo, lineas);
  const guid = crypto.randomBytes(16).toString('hex');
  fs.writeFileSync(`${destino}/${guid}.pdf`, buf);
  return {
    original: nombre,
    enDisco: `${guid}.pdf`,
    bytes: buf.length,
    sha256: crypto.createHash('sha256').update(buf).digest('hex')
  };
});

console.log(JSON.stringify(salida, null, 2));
