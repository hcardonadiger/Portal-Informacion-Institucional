// Persistencia: portal .NET (SQL Server) — sin Supabase.
// El servidor inyecta window.__EXP__ (datos a editar) y window.__EXPMETA__ ({id, postUrl, token, indexUrl}).
var expCache = [];
var currentIdx = null;
var tramiteCount = 1;
var activeTram = 0;
var TRAM_COLORS = [
  {bg:'#dde9ff',fg:'#1455a4'},{bg:'#ede9fe',fg:'#6d28d9'},{bg:'#dcfce7',fg:'#15803d'},
  {bg:'#fff7ed',fg:'#c2410c'},{bg:'#fce7f3',fg:'#be185d'},{bg:'#ecfeff',fg:'#0e7490'},
  {bg:'#fef9c3',fg:'#92400e'},{bg:'#ffe4e6',fg:'#be123c'},{bg:'#f0fdf4',fg:'#166534'},
  {bg:'#fdf4ff',fg:'#7e22ce'}
];

// Flow data: flujosActual[tramIdx] = [{tipo,titulo,area,tiempo,doc_emitido,obs,retorno_a}]
var flujosActual = [[]];
var flujosPropuesto = [[]];

// Requisitos por trámite: reqsTram[t] = [{requisito,obs}]; acciones alineadas en accionesTram[t]
var reqsTram = [[]];
var accionesTram = [[]];

// ── CATÁLOGOS TGR (Clasificador oficial SEFIN — tgr1.sefin.gob.hn) ──
var TGR_INST = [
  '1 - Congreso Nacional','2 - Tribunal Superior de Cuentas',
  '3 - Comisionado Nacional de los Derechos Humanos (CONADEH)','10 - Poder Judicial',
  '20 - Presidencia de la República','22 - Fondo Hondureño de Inversión Social (FHIS)',
  '24 - Instituto de la Propiedad (IP)','26 - Instituto Nacional de la Juventud',
  '28 - Instituto Nacional de Conservación y Desarrollo Forestal (ICF)','30 - Secretaría de la Presidencia',
  '32 - Instituto de Acceso a la Información Pública (IAIP)','33 - Comisión Nacional de Vivienda y Asentamientos Humanos (CONVIVIENDA)',
  '35 - Instituto Hondureño de Geología y Minas (INHGEOMIN)','37 - Servicio de Administración de Rentas (SAR)',
  '38 - Dirección Adjunta de Rentas Aduaneras (DARA)','40 - Secretaría de Gobernación, Justicia y Descentralización (SGJD)',
  '41 - Comisión Permanente de Contingencias (COPECO)','42 - Cuerpo de Bomberos de Honduras',
  '43 - Empresa Nacional de Artes Gráficas (ENAG)','44 - Instituto Nacional Penitenciario (INP)',
  '45 - Instituto Nacional de Migración (INM)','50 - Secretaría de Educación (SEDUC)',
  '60 - Secretaría de Salud (SESAL)','61 - Ente Regulador de Servicios de Agua Potable y Saneamiento (ERSAPS)',
  '62 - Agencia de Regulación Sanitaria (ARSA)','70 - Secretaría de Seguridad (SESEGU)',
  '80 - Secretaría de Relaciones Exteriores y Cooperación Internacional (SRECI)','90 - Secretaría de Defensa Nacional (SEDENA)',
  '91 - Agencia Hondureña de Aeronáutica Civil (AHAC)','100 - Secretaría de Finanzas (SEFIN)',
  '101 - Comisión Nacional de Telecomunicaciones (CONATEL)','102 - Comisión Administradora Zona Libre Turística (ZOLITUR)',
  '103 - Dirección Nacional de Bienes del Estado (DNBE)','120 - Secretaría de Infraestructura y Servicios Públicos (INSEP)',
  '121 - Dirección General de la Marina Mercante (DGMM)','122 - Fondo Vial',
  '123 - Instituto Hondureño de Transporte Terrestre (IHTT)','130 - Secretaría de Trabajo y Seguridad Social (STSS)',
  '140 - Secretaría de Agricultura y Ganadería (SAG)','141 - Dirección de Ciencia y Tecnología Agropecuaria (DICTA)',
  '145 - Servicio Nacional de Sanidad e Inocuidad Agroalimentaria (SENASA)','150 - Secretaría de Energía, Recursos Naturales, Ambiente y Minas (MiAmbiente)',
  '153 - Comisión Reguladora de Energía Eléctrica (CREE)'
];
var TGR_RUBROS = [
  '11401 - A casinos de juego envite o azar','11402 - A la venta de timbres de contratación',
  '11404 - A servicios de vías públicas','11405 - Sobre traspaso de vehículos',
  '11406 - Sobre servicios turísticos','12102 - Control migratorio',
  '12103 - Inspección de vehículos','12107 - Marchamos','12108 - Servicios consulares',
  '12109 - Papeles de aduana','12114 - Papel notarial','12118 - Registros ley de propiedad',
  '12120 - Servicios de auténticas y traducciones','12121 - Emisión de constancias, certificaciones y otros',
  '12126 - Actos administrativos','12199 - Tasas varias','12201 - Libreta pasaporte',
  '12203 - Registro marcas de fábricas','12204 - Registro patente de invención',
  '12205 - Registro de prestamistas','12206 - Incorporación de empresas mercantiles',
  '12208 - Licencia de conducir','12209 - Otras licencias','12211 - Permisos y renovaciones migratorias',
  '12213 - Registro nacional de armas','12217 - Emisión y reposición de placas y calcomanías',
  '12218 - Registro nacional de las personas','12219 - Registro derechos de autor y conexos',
  '12299 - Derechos varios','12301 - Concesiones y frecuencias radioeléctricas','12399 - Regalías varias'
];
// Nombres de rubros adicionales (no incluidos en la lista general, usados en mapeos por institución)
var TGR_RUBRO_EXTRA = {
  '12806':'Devoluciones de ejercicios fiscales anteriores por pagos en exceso'
};
// Lookup código → nombre, construido desde TGR_RUBROS + extras
var TGR_RUBRO_NAMES = (function(){
  var m = {};
  TGR_RUBROS.forEach(function(s){ var p=s.indexOf(' - '); if(p>0) m[s.slice(0,p).trim()] = s.slice(p+3); });
  Object.keys(TGR_RUBRO_EXTRA).forEach(function(c){ m[c]=TGR_RUBRO_EXTRA[c]; });
  return m;
})();
// Mapeo institución (código) → rubros permitidos. Fuente: portal TGR-1 (tgr1.sefin.gob.hn).
// Las instituciones sin lista específica muestran el catálogo completo como respaldo.
var TGR_INST_RUBROS = {
  '1':   ['11402','12121','12806'],                                                                        // Congreso Nacional
  '2':   ['11402','12121','12499','12804','12806'],                                                        // Tribunal Superior de Cuentas
  '3':   ['11402','12121','12806'],                                                                        // Comisionado Nacional de Derechos Humanos
  '10':  ['11402','12121','12806'],                                                                        // Poder Judicial
  '20':  ['11402','12121','12806'],                                                                        // Presidencia de la República
  '21':  ['11402','12121','12806'],                                                                        // Programa de Asignación Familiar
  '22':  ['11402','12121','12806'],                                                                        // Fondo Hondureño de Inversión Social
  '23':  ['11402','12121','12806'],                                                                        // Consejo Hondureño de Ciencia y Tecnología
  '24':  ['11402','12118','12121','12203','12204','12219','12806','15101'],                                 // Instituto de la Propiedad
  '25':  ['11402','12121','12806'],                                                                        // Prog. Nac. de Prevención Rehabilitación y Reinserción Social
  '26':  ['11402','12121','12806'],                                                                        // Instituto Nacional de la Juventud
  '27':  ['11402','12121','12806'],                                                                        // Vice Presidencia de la República
  '28':  ['11402','12121','12199','12499','12806','15101','15104'],                                        // Instituto Nacional de Conservación y Desarrollo Forestal
  '29':  ['11402','12121','12806','12899'],                                                                // Comisión para la Promoción de la Alianza Público-Privada
  '30':  ['11402','12121','12806'],                                                                        // Secretaría de la Presidencia
  '31':  ['11402','12121','12806','15299'],                                                                // Inversión Estratégica de Honduras (INVEST-HONDURAS)
  '32':  ['11402','12121','12806'],                                                                        // Instituto de Acceso a la Información Pública
  '33':  ['11402','12121','12806'],                                                                        // Programa de Vivienda y Asentamientos Humanos
  '34':  ['11402','12121','12806'],                                                                        // Despacho Ministerial de Socialización y Acomp. Digital
  '35':  ['11402','12116','12117','12121','12302','12306','12417','12806','12899','15101','17603'],         // Instituto Hondureño de Geología y Minas
  '36':  ['11402','12121','12806','15207','17603'],                                                        // Dirección Ejecutiva de Cultura, Artes y Deportes
  '37':  ['11402','12121','12806','12899'],                                                                // Servicio de Administración de Rentas
  '38':  ['11402','12806','12899'],                                                                        // Dirección Adjunta de Rentas Aduaneras
  '39':  ['11402','12121'],                                                                                // Servicio Nacional de Emprendimiento y Pequeños Negocios
  '40':  ['11402','12120','12121','12211','12806','12899'],                                                // Secretaría de Gobernación, Justicia y Descentralización
  '41':  ['11402','12121','12806'],                                                                        // Dirección General de Investigación y Evaluación del Estado
  '42':  ['11402','12121','12806','15299'],                                                                // Registro Nacional de las Personas
  '43':  ['11402','12121','12806','15204'],                                                                // Instituto Nacional de Estadística
  '44':  ['11402','12121','12806','15101'],                                                                // Instituto Nacional Agrario
  '45':  ['11402','12102','12121','12201','12211','12416','12806','12899'],                                 // Instituto Nacional de Migración
  '46':  ['11402','12121','12806','15101'],                                                                // Dirección General de Biodiversidad
  '50':  ['11402','12121','12806','15204','15207','17605'],                                                // Secretaría de Seguridad
  '51':  ['11402','12121','12806','12899','15299'],                                                        // Centro Nacional de Educación para el Trabajo (CENET)
  '60':  ['11402','12121','12405','12806','15206'],                                                        // Secretaría de Educación
  '61':  ['11402','12121','12806','15299'],                                                                // Ente Regulador de Servicios de Agua Potable y Saneamiento
  '62':  ['12121','12199','12203','12209','12499','12806','15299'],                                        // Agencia de Regulación Sanitaria
  '70':  ['11402','12120','12121','12208','12213','12413','12806','12899','15205'],                         // Secretaría de Agricultura y Ganadería
  '71':  ['11402','12121','12804','12806'],                                                                // Dirección de Ciencia y Tecnología Agropecuaria
  '72':  ['11402','12121','12806'],                                                                        // Servicio Nacional de Sanidad Agropecuaria
  '80':  ['11402','12108','12120','12121','12201','12218','12806'],                                         // Secretaría de Relaciones Exteriores y Cooperación Internacional
  '90':  ['11402','12121','12499','12806','15104','15203','15207','15299','17601','17603'],                 // Secretaría de Defensa Nacional
  '91':  ['11402','11409','12115','12121','12806'],                                                        // Agencia Hondureña de Aeronáutica Civil
  '100': ['11402','12121','12499','12806','12899'],                                                        // Secretaría de Finanzas
  '101': ['11402','12121','12124','12301','12412','12806'],                                                // Comisión Nacional de Telecomunicaciones
  '102': ['11110','11402','12121','12123','12209','12806','15101'],                                        // Comis. Administradora Zona Libre Turística Islas de la Bahía
  '103': ['11402'],                                                                                        // Dirección General de Marina Mercante
  '104': ['11402','12121','12801','12806','12899'],                                                        // Servicio Nacional de Acueductos y Alcantarillados (SANAA)
  '110': ['11402'],                                                                                        // Secretaría de Obras Públicas, Transporte y Vivienda
  '111': ['11402'],                                                                                        // Fondo Vial
  '120': ['11402','11409','12121','12299','12409','12806','12899'],                                        // Secretaría de Salud
  '121': ['11402','12105','12121','12202','12207','12806'],                                                // Instituto Hondureño de Seguridad Social (IHSS)
  '122': ['11402'],                                                                                        // Dirección de Desarrollo y Fortalecimiento de Gobiernos Locales
  '123': ['11402','11409','12121','12409','12806'],                                                        // Instituto Nacional de Jubilaciones y Pensiones de Empleados
  '130': ['11402','12121','12410','12804','12806','15204'],                                                // Secretaría de Trabajo y Seguridad Social
  '140': ['11402','12121','12199','12209','12309','12499','12801','12804','12806','12899','15102','15299','17601','17603','17604','18105','18302'], // Secretaría de Recursos Naturales y Ambiente
  '141': ['11402','12121','12806','15102','15299','17603'],                                                // Dirección de Evaluación y Control Ambiental
  '142': ['11402'],                                                                                        // Dirección General de Energía
  '143': ['11402'],                                                                                        // Comisión Nacional de Energía
  '144': ['11402','12121','12806','15101'],                                                                // Empresa Nacional de Energía Eléctrica (ENEE)
  '145': ['11402','12121','12199','12499','12806','15104'],                                                // Empresa Hondureña de Telecomunicaciones (HONDUTEL)
  '150': ['11402','12117','12120','12121','12199','12209','12299','12304','12406','12499','12806','12807','12899','15104','15217','17603'], // Secretaría de Infraestructura y Servicios Públicos
  '151': ['11402'],                                                                                        // Dirección General de Protección al Consumidor
  '152': ['11402'],                                                                                        // Instituto de Previsión Militar
  '153': ['11402','12121','12806','15299'],                                                                // Instituto de la Propiedad Industrial
  '160': ['11402','12121','12806'],                                                                        // Secretaría de Desarrollo Económico
  '161': ['11402'],                                                                                        // Dirección General de la Marina Mercante
  '170': ['11402'],                                                                                        // Secretaría de Turismo
  '180': ['11402','12218','12806'],                                                                        // Secretaría de Educación Superior, Ciencia y Tecnología
  '190': ['11402','12121','12806'],                                                                        // Secretaría de Desarrollo Social
  '200': ['11402','12121','12404','12410','12499','12804','12806'],                                        // Universidad Nacional Autónoma de Honduras (UNAH)
  '201': ['11402','12121','12806'],                                                                        // Universidad Pedagógica Nacional Francisco Morazán
  '210': ['11402','12121','12499','12806'],                                                                // Universidad Nacional de Ciencias Forestales
  '220': ['11402'],                                                                                        // Escuela Nacional de Ciencias Forestales
  '230': ['11402'],                                                                                        // Escuela Agrícola Panamericana El Zamorano
  '240': ['11402','12121','12806'],                                                                        // Universidad de San Pedro Sula
  '241': ['11402'],                                                                                        // Universidad Tecnológica Centroamericana
  '244': ['11402','12121','12806','17603'],                                                                // Universidad Nacional de Ingeniería
  '250': ['11402'],                                                                                        // Centro Universitario Regional del Litoral Atlántico
  '260': ['11402'],                                                                                        // Universidad Católica de Honduras
  '270': ['11402','12121','12806'],                                                                        // Consejo Nacional de Educación Superior
  '280': ['11402','12121','12806','15299'],                                                                // Comisión para la Defensa y Promoción de la Competencia
  '281': ['11402','12121','12806'],                                                                        // Superintendencia del Sistema Financiero
  '290': ['11402','11406','12121','12199','12499','12806','15199','15299'],                                 // Comisión Nacional de Bancos y Seguros
  '310': ['11402','12121','12199','12203','12209','12806'],                                                // Instituto Hondureño del Café (IHCAFE)
  '320': ['11402'],                                                                                        // Cámara de Comercio e Industrias de Tegucigalpa
  '330': ['11402'],                                                                                        // Consejo Hondureño de la Empresa Privada
  '340': ['11402'],                                                                                        // Federación de Municipios del Istmo Centroamericano
  '350': ['11402'],                                                                                        // Asociación Nacional de Municipios de Honduras
  '360': ['11402'],                                                                                        // Municipalidad de Tegucigalpa
  '370': ['11402'],                                                                                        // Municipalidad de San Pedro Sula
  '380': ['11402'],                                                                                        // Municipalidad de La Ceiba
  '390': ['11402'],                                                                                        // Otras Municipalidades
  '404': ['12121','12806','15101'],                                                                        // Dirección General de Pesca y Acuicultura
  '409': ['11402','12121','12806','15299'],                                                                // Instituto Hondureño de Turismo (IHT)
  '410': ['11402','12121','12806','15207','17603'],                                                        // Dirección Nacional de Vialidad
  '411': ['11402','11409','12121','12299','12409','12806','12899'],                                        // Colegio Médico de Honduras
  '412': ['11402','12121','12806'],                                                                        // Colegio de Abogados de Honduras
  '413': ['12121','12806','15299','17603','17605'],                                                        // Colegio de Ingenieros Civiles de Honduras
  '414': ['11402','12121','12806'],                                                                        // Colegio de Economistas de Honduras
  '415': ['11402','12121','12806'],                                                                        // Colegio de Arquitectos de Honduras
  '419': ['11402','12121','12806','17603'],                                                                // Colegio de Ingenieros Mecánicos, Eléctricos y Químicos
  '422': ['12121','12806'],                                                                                // Colegio de Peritos Mercantiles y Contadores Públicos
  '449': ['11402','12121','12205','12206','12404','12499','12801','12804','12806','12807','12899','15101','17101','17102','17201','17202','17203','17204','17205','17206','17207','17301','17302','17401','17402','17501','17601','17602','17603','17604','17605','17701','17801','18101','18102','18103','18104','18105','18106','18107','18108','18109','18110','18111','18112','18113','18114','18401','18402','18403','18404'], // Secretaría de Finanzas - Cuenta Única del Tesoro
  '500': ['11203','11402','12121','12806'],                                                                // Municipalidad del Distrito Central
  '501': ['11402'],                                                                                        // Alcaldía Municipal de Choluteca
  '503': ['11402','12121','12806'],                                                                        // Alcaldía Municipal de Comayagua
  '504': ['11402','12121','12806'],                                                                        // Alcaldía Municipal de Juticalpa
  '505': ['11402','12121','12806'],                                                                        // Alcaldía Municipal de Gracias
  '506': ['11402','12121','12806'],                                                                        // Alcaldía Municipal de Yoro
  '507': ['11402','12121','12806'],                                                                        // Alcaldía Municipal de Santa Rosa de Copán
  '508': ['11402','12121','12806'],                                                                        // Alcaldía Municipal de Siguatepeque
  '509': ['11402','12121','12806'],                                                                        // Alcaldía Municipal de Danlí
  '510': ['11402','12121','12806'],                                                                        // Alcaldía Municipal de Puerto Cortés
  '511': ['11402','12121','12806'],                                                                        // Alcaldía Municipal de Tela
  '512': ['11402','12121','12806'],                                                                        // Alcaldía Municipal de Trujillo
  '513': ['11402','12121','12806'],                                                                        // Alcaldía Municipal de Roatán
  '514': ['11402','12121','12199','12499','12806'],                                                        // Policía Nacional
  '515': ['11402','12121','12806'],                                                                        // Alcaldía Municipal de Tocoa
  '601': ['11402','12121','12806'],                                                                        // Alcaldía Municipal de Catacamas
  '602': ['11402','12121','12806'],                                                                        // Alcaldía Municipal de Olanchito
  '603': ['11402','12121','12806'],                                                                        // Alcaldía Municipal de El Progreso
  '604': ['11402','12121','12806'],                                                                        // Alcaldía Municipal de Villanueva
  '605': ['11402','12121','12806'],                                                                        // Alcaldía Municipal de Choloma
  '701': ['11402','12121','12806'],                                                                        // Alcaldía Municipal de La Lima
  '702': ['11402','12121','12806'],                                                                        // Alcaldía Municipal de Potrerillos
  '703': ['11402','12121','12806'],                                                                        // Alcaldía Municipal de San Manuel
  '704': ['11402','12121','12806'],                                                                        // Alcaldía Municipal de Pimienta
  '705': ['11402','12121','12806'],                                                                        // Alcaldía Municipal de Santa Cruz de Yojoa
  '706': ['11402','12121','12806'],                                                                        // Alcaldía Municipal de Armenia Copán
  '707': ['11402','12121','12806']                                                                         // Alcaldía Municipal de San Pedro Sula Norte
};
function tgrCodeOf(instValue){ var m=String(instValue||'').match(/^\s*(\d+)/); return m?m[1]:''; }
function rubroLabel(code){ return code + ' - ' + (TGR_RUBRO_NAMES[code] || ('Rubro '+code)); }
function tgrInstOptions(){
  return '<option value="">— Seleccione institución —</option>'
    + TGR_INST.map(function(v){ return '<option>'+escHtml(v)+'</option>'; }).join('')
    + '<option>Otra (especificar en observaciones)</option>';
}
function tgrRubroOptionsFor(instValue){
  var code = tgrCodeOf(instValue);
  var list = TGR_INST_RUBROS[code];
  var opts = '<option value="">— Seleccione rubro —</option>';
  if(list && list.length){
    opts += list.map(function(c){ return '<option>'+escHtml(rubroLabel(c))+'</option>'; }).join('');
  } else {
    opts += TGR_RUBROS.map(function(v){ return '<option>'+escHtml(v)+'</option>'; }).join('');
  }
  return opts + '<option>Otro (especificar en observaciones)</option>';
}
function onTgrInstChange(i){
  var sel = document.getElementById('tgr_rubro_'+i);
  if(sel) sel.innerHTML = tgrRubroOptionsFor(gv('tgr_inst_'+i));
}
function togglePago(i){
  var m = gv('metodo_pago_'+i);
  var dep = document.getElementById('pago-deposito-'+i);
  var tgr = document.getElementById('pago-tgr-'+i);
  if(dep) dep.style.display = (m==='Depósito bancario') ? '' : 'none';
  if(tgr) tgr.style.display = (m==='TGR') ? '' : 'none';
}

// ── REQUISITOS POR TRÁMITE ───────────────────────────────────
function renderReqFichaRows(i){
  var tbody = document.getElementById('req-ficha-tbody-'+i);
  if(!tbody) return;
  var rows = reqsTram[i] || (reqsTram[i]=[]);
  tbody.innerHTML = rows.map(function(r, idx){
    return '<tr>'
      + '<td class="nc">'+(idx+1)+'</td>'
      + '<td><input type="text" placeholder="Nombre o descripción del requisito" value="'+escHtml(r.requisito||'')+'" onchange="updateReqFicha('+i+','+idx+',\'requisito\',this.value)"></td>'
      + '<td><textarea placeholder="Observación..." onchange="updateReqFicha('+i+','+idx+',\'obs\',this.value)">'+escHtml(r.obs||'')+'</textarea></td>'
      + '<td style="text-align:center;padding:6px"><button class="btn-rm" onclick="removeReqFicha('+i+','+idx+')">✕</button></td>'
    + '</tr>';
  }).join('');
}
function actualizarNumReq(){
  var total = 0;
  reqsTram.forEach(function(arr){ if(arr) arr.forEach(function(r){ if((r.requisito||'').trim()) total++; }); });
  var el = document.getElementById('num_req');
  if(el) el.value = total || '';
}
function addReqFicha(i){ if(!reqsTram[i]) reqsTram[i]=[]; reqsTram[i].push({requisito:'',obs:''}); renderReqFichaRows(i); actualizarNumReq(); }
function updateReqFicha(i,idx,field,val){ if(reqsTram[i]&&reqsTram[i][idx]){ reqsTram[i][idx][field]=val; if(field==='requisito') actualizarNumReq(); } }
function removeReqFicha(i,idx){ if(reqsTram[i]){ reqsTram[i].splice(idx,1); renderReqFichaRows(i); actualizarNumReq(); } }

// ── INFRAESTRUCTURA SOL (una vez por expediente) ─────────────
var PERFILES = [
  'Administrador/a de redes','Administrador/a de base de datos',
  'Administrador/a de servidores','Especialista en ciberseguridad',
  'Desarrollador/a o configurador/a SOL','Soporte técnico y mesa de ayuda'
];
var DATACENTER_COND = [
  'Energía redundante / UPS','Climatización controlada','Copias de seguridad',
  'Monitoreo 24/7','Firewall y segmentación','Plan de recuperación ante desastres'
];
var INFRA_REQS = [
  { grupo:'Servidor de base de datos — Hardware', items:[
    '2 procesadores Intel Xeon Silver 4210R o capacidad equivalente',
    '64 GB RAM (2 x 32 GB RDIMM, 3200 MT/s)',
    'RAID 10 con controlador equivalente a PERC H330',
    '4 discos de 1.2 TB SAS 10K RPM',
    'Fuente redundante hot-plug 1+1, 495 W','Red dual port 10 Gb Base-T' ]},
  { grupo:'Servidor de base de datos — Software', items:[
    'Windows Server 2019 Standard, 16 CORE o versión compatible',
    'Licenciamiento adicional de Windows Server por núcleos',
    'Microsoft SQL Server 2022 Standard por núcleos','Cantidad mínima: 1 servidor' ]},
  { grupo:'Servidor de aplicaciones web — Hardware', items:[
    '2 procesadores Intel Xeon Silver 4210R o capacidad equivalente',
    '64 GB RAM (2 x 32 GB RDIMM, 3200 MT/s)',
    'RAID 1 con controlador equivalente a PERC H330',
    '2 discos de 1.2 TB SAS 10K RPM',
    'Fuente redundante hot-plug 1+1, 495 W','Red dual port 10 Gb Base-T' ]},
  { grupo:'Servidor de aplicaciones web — Software', items:[
    'Windows Server 2019 Standard, 16 CORE o versión compatible',
    'Licenciamiento adicional de Windows Server por núcleos','Cantidad mínima: 1 servidor' ]},
  { grupo:'Equivalencia para servidores virtuales', items:[
    'Núcleos de procesamiento al menos coincidentes','Memoria RAM al menos coincidente',
    'Almacenamiento disponible al menos coincidente','Software y licenciamiento vigentes' ]}
];
var INFRA_STATUS = ['Pendiente','Cumple','No cumple','Parcial','No aplica'];

function renderInfraAll(){ renderPerfilesInfra(); renderDatacenterCond(); renderInfraChecklist(); }

function renderPerfilesInfra(){
  var g = document.getElementById('perfiles-grid');
  if(!g) return;
  g.innerHTML = PERFILES.map(function(p,k){
    return '<div class="perfil-item" id="perfil_item_'+k+'">'
      + '<div class="perfil-head" onclick="togglePerfil('+k+')">'
        + '<input type="checkbox" id="perfil_chk_'+k+'" style="display:none">'
        + '<div class="perfil-cb" id="perfil_cb_'+k+'"></div>'
        + '<div class="perfil-name">'+escHtml(p)+'</div>'
      + '</div>'
      + '<div class="perfil-det" id="perfil_det_'+k+'" style="display:none">'
        + '<input type="text" id="perfil_nom_'+k+'" placeholder="Nombre de la persona" onclick="event.stopPropagation()">'
        + '<input type="email" id="perfil_mail_'+k+'" placeholder="Correo institucional" onclick="event.stopPropagation()">'
      + '</div>'
    + '</div>';
  }).join('');
}
function togglePerfil(k, forceState){
  var chk = document.getElementById('perfil_chk_'+k);
  if(!chk) return;
  if(forceState===undefined) chk.checked = !chk.checked; else chk.checked = forceState;
  var item = document.getElementById('perfil_item_'+k);
  var cb   = document.getElementById('perfil_cb_'+k);
  var det  = document.getElementById('perfil_det_'+k);
  if(item) item.classList.toggle('checked', chk.checked);
  if(cb) cb.textContent = chk.checked ? '✓' : '';
  if(det) det.style.display = chk.checked ? '' : 'none';
}
function renderDatacenterCond(){
  var g = document.getElementById('datacenter-grid');
  if(!g) return;
  g.className = 'perfiles-grid';
  g.innerHTML = DATACENTER_COND.map(function(c,k){
    return '<div class="perfil-item" id="dc_item_'+k+'">'
      + '<div class="perfil-head" onclick="toggleDcCond('+k+')">'
        + '<input type="checkbox" id="dc_cond_'+k+'" style="display:none">'
        + '<div class="perfil-cb" id="dc_cb_'+k+'"></div>'
        + '<div class="perfil-name">'+escHtml(c)+'</div>'
      + '</div>'
    + '</div>';
  }).join('');
}
function toggleDcCond(k, forceState){
  var chk = document.getElementById('dc_cond_'+k);
  if(!chk) return;
  if(forceState===undefined) chk.checked = !chk.checked; else chk.checked = forceState;
  var item = document.getElementById('dc_item_'+k);
  var cb   = document.getElementById('dc_cb_'+k);
  if(item) item.classList.toggle('checked', chk.checked);
  if(cb) cb.textContent = chk.checked ? '✓' : '';
}
function renderInfraChecklist(){
  var wrap = document.getElementById('infra-checklist');
  if(!wrap) return;
  var html = '';
  INFRA_REQS.forEach(function(grp, gi){
    html += '<div style="font-size:11.5px;font-weight:800;color:var(--azul-m);text-transform:uppercase;letter-spacing:.05em;margin:.9rem 0 .5rem">'+escHtml(grp.grupo)+'</div>';
    grp.items.forEach(function(item, ii){
      var sid = 'infra_st_'+gi+'_'+ii, oid = 'infra_ob_'+gi+'_'+ii;
      html += '<div style="display:grid;grid-template-columns:1fr 130px 200px;gap:8px;align-items:center;padding:.4rem 0;border-bottom:1px solid #f0f2f7">'
        + '<span style="font-size:12px;color:var(--texto)">'+escHtml(item)+'</span>'
        + '<select id="'+sid+'" style="padding:6px 8px;border:1.5px solid var(--borde);border-radius:7px;font-size:12px;font-family:inherit">'
          + INFRA_STATUS.map(function(s){ return '<option>'+s+'</option>'; }).join('') + '</select>'
        + '<input type="text" id="'+oid+'" placeholder="Capacidad actual / brecha / evidencia" style="padding:6px 8px;border:1.5px solid var(--borde);border-radius:7px;font-size:12px;font-family:inherit">'
      + '</div>';
    });
  });
  wrap.innerHTML = html;
}
function toggleVirtOtro(){
  var v = gv('infra_dc_virt');
  var w = document.getElementById('virt-otro-wrap');
  if(w) w.style.display = (v==='Otro') ? '' : 'none';
}
function recolectarInfra(){
  var perfiles = [];
  PERFILES.forEach(function(p,k){
    var chk = document.getElementById('perfil_chk_'+k);
    if(chk && chk.checked) perfiles.push({perfil:p, nombre:gv('perfil_nom_'+k), correo:gv('perfil_mail_'+k)});
  });
  var cond = [];
  DATACENTER_COND.forEach(function(c,k){ var el=document.getElementById('dc_cond_'+k); if(el&&el.checked) cond.push(c); });
  var checklist = [];
  INFRA_REQS.forEach(function(grp,gi){ grp.items.forEach(function(item,ii){
    checklist.push({req:item, status:gv('infra_st_'+gi+'_'+ii)||'Pendiente', obs:gv('infra_ob_'+gi+'_'+ii)});
  }); });
  var pr = document.querySelector('input[name="infra_personal"]:checked');
  var ac = document.querySelector('input[name="infra_acomp"]:checked');
  return {
    personal: pr?pr.value:'', perfiles:perfiles, personal_ti:gv('infra_personal_ti'), resp_sol:gv('infra_resp_sol'),
    acomp: ac?ac.value:'', dc_modalidad:gv('infra_dc_modalidad'), dc_virt:gv('infra_dc_virt'), dc_virt_otro:gv('infra_dc_virt_otro'),
    dc_disp:gv('infra_dc_disp'), dc_cond:cond, dc_obs:gv('infra_dc_obs'), checklist:checklist, plan:gv('infra_plan')
  };
}
function poblarInfra(inf){
  inf = inf || {};
  renderInfraAll();
  if(inf.personal){ var p=document.querySelector('input[name="infra_personal"][value="'+inf.personal+'"]'); if(p)p.checked=true; }
  if(inf.acomp){ var a=document.querySelector('input[name="infra_acomp"][value="'+inf.acomp+'"]'); if(a)a.checked=true; }
  (inf.perfiles||[]).forEach(function(pf){
    var k = PERFILES.indexOf(pf.perfil);
    if(k>=0){ togglePerfil(k, true); sv('perfil_nom_'+k,pf.nombre||''); sv('perfil_mail_'+k,pf.correo||''); }
  });
  sv('infra_personal_ti',inf.personal_ti||''); sv('infra_resp_sol',inf.resp_sol||'');
  sv('infra_dc_modalidad',inf.dc_modalidad||''); sv('infra_dc_virt',inf.dc_virt||''); toggleVirtOtro(); sv('infra_dc_virt_otro',inf.dc_virt_otro||'');
  sv('infra_dc_disp',inf.dc_disp||''); sv('infra_dc_obs',inf.dc_obs||''); sv('infra_plan',inf.plan||'');
  (inf.dc_cond||[]).forEach(function(c){ var k=DATACENTER_COND.indexOf(c); if(k>=0) toggleDcCond(k, true); });
  var ci=0;
  INFRA_REQS.forEach(function(grp,gi){ grp.items.forEach(function(item,ii){
    var row=(inf.checklist||[])[ci++]; if(row){ sv('infra_st_'+gi+'_'+ii,row.status||'Pendiente'); sv('infra_ob_'+gi+'_'+ii,row.obs||''); }
  }); });
}

// ── NAVEGACIÓN ──────────────────────────────────────────────
function ir(n){
  if(n === 5) renderModeloReqs(activeTram);
  document.querySelectorAll('.sec').forEach(function(s){ s.classList.remove('active'); });
  document.querySelectorAll('.sbi').forEach(function(s){ s.classList.remove('active'); });
  document.getElementById('sec'+n).classList.add('active');
  document.getElementById('sb'+n).classList.add('active');
  window.scrollTo({top:0,behavior:'smooth'});
}

// ── META SIDEBAR ────────────────────────────────────────────
function actualizarMeta(){
  var inst = gv('inst') || '—';
  var cod = gv('codigo_exp') || '—';
  var analista = gv('analista') || '—';
  var fecha = gv('fecha_apertura') || '—';
  document.getElementById('sb-inst').textContent = inst;
  document.getElementById('sb-codigo').textContent = cod;
  document.getElementById('sb-analista').textContent = analista;
  document.getElementById('sb-fecha').textContent = fecha;
  // El topbar del HTML original no existe en el port Razor (se usa el topnav del _Layout).
  var tb = document.getElementById('topbar-title');
  if (tb) tb.textContent = inst !== '—' ? 'Expediente — ' + inst : 'Expediente de Digitalización — DIGER';
}

// ── CÓDIGO AUTOMÁTICO ───────────────────────────────────────
function generarCodigo(){
  var inst = gv('inst');
  if(!inst) return;
  var cod = document.getElementById('codigo_exp');
  if(cod.value && cod.dataset.manual) return;
  var abbr = inst.replace(/[^A-Za-z]/g,'').toUpperCase().slice(0,4);
  var year = new Date().getFullYear();
  var all = cargarTodos();
  var seq = String((currentIdx !== null ? currentIdx : all.length) + 1).padStart(2,'0');
  cod.value = 'EXP-' + abbr + '-' + year + '-' + seq;
  actualizarMeta();
  actualizarCodsTramite();
  filtrarContactosPorInstitucion();
}

var _todosContactosAsis = [];
var _contactosAsis = [];

var _INST_NORM = {
  'ihadfa':'IHADFA','consucoop':'CONSUCOOP','conscucoop':'CONSUCOOP',
  'instituto de la propiedad':'IP','instituto propiedad':'IP','ip':'IP',
  'canaturh/iht':'CANATURH / IHT','canaturh':'CANATURH / IHT','canaturh / iht':'CANATURH / IHT',
  'banhprovi':'BANHPROVI','cnbs':'CNBS','conatel':'CONATEL','convivienda':'CONVIVIENDA',
  'copeco':'COPECO','ihcine':'IHCINE','ihtt':'IHTT','inprema':'INPREMA','inpreunah':'INPREUNAH',
  'sag':'SAG','secapph':'SECAPPH','sen':'SEN','sgjd':'SGJD','sit':'SIT','sreci':'SRECI','serna':'SERNA'
};
function _normInst(val){
  if(!val) return '';
  var v = val.trim();
  if(/^IP\b/i.test(v) && v.length <= 20) return 'IP';
  return _INST_NORM[v.toLowerCase().replace(/\s+/g,' ')] || v.toUpperCase().replace(/\s+/g,' ');
}

function cargarContactosAsistencias(){
  // La carga real ocurre por institución desde el Directorio de Contactos del portal.
  filtrarContactosPorInstitucion();
}

// Consulta el Directorio de Contactos del portal por la institución seleccionada.
async function filtrarContactosPorInstitucion(){
  var sel = document.getElementById('contacto-buscar');
  if(!sel) return;
  var inst = gv('inst');
  var meta = window.__EXPMETA__ || {};
  sel.innerHTML = '<option value="">— Seleccionar enlace del directorio —</option>';
  _contactosAsis = [];
  if(!inst || !meta.contactosUrl) return;
  try{
    var sep = meta.contactosUrl.indexOf('?') >= 0 ? '&' : '?';
    var resp = await fetch(meta.contactosUrl + sep + 'institucion=' + encodeURIComponent(inst));
    if(!resp.ok) return;
    _contactosAsis = await resp.json();
  }catch(e){ _contactosAsis = []; }

  if(_contactosAsis.length){
    _contactosAsis.forEach(function(c,idx){
      var opt = document.createElement('option');
      opt.value = idx;
      opt.textContent = (c.nombre||'') + (c.cargo ? ' — '+c.cargo : '');
      sel.appendChild(opt);
    });
  } else {
    var opt2 = document.createElement('option');
    opt2.disabled = true;
    opt2.textContent = 'Sin contactos para esta institución';
    sel.appendChild(opt2);
  }
}

function seleccionarContacto(sel){
  var idx = parseInt(sel.value);
  if(isNaN(idx) || idx < 0) return;
  var c = _contactosAsis[idx];
  if(!c) return;
  sv('contacto_nombre', c.nombre||'');
  sv('contacto_cargo', c.cargo||'');
  sv('contacto_correo', c.correo||'');
  sv('contacto_tel', c.telefono||'');
}

// ── TRAMITES DINÁMICOS ──────────────────────────────────────
// Campos de la ficha que viven en el DOM (para snapshot al agregar/quitar trámites)
var FICHA_FIELDS = ['nombre_corto','modalidad','plazo_legal','tercero','tiempo_real','metodo_pago',
  'pago_banco','pago_cuenta','tgr_inst','tgr_rubro','tgr_monto','doc_entregado','objetivo',
  'alcance_obs','descripcion','dirigido','horario','telefono','email_tramite','sitio_web'];

function tramRowHTML(i){
  var rm = tramiteCount > 1
    ? '<button type="button" class="btn-rm" title="Quitar trámite" style="align-self:start;margin-top:24px" onclick="quitarTramiteApertura('+i+')">✕</button>'
    : '';
  return '<div class="tram-row" style="flex-direction:row;align-items:flex-start;gap:10px">'
    + '<div class="f" style="flex:1"><label>Trámite ' + (i+1) + ' <span class="star">*</span>'
    + ' <span class="tram-cod" id="tcod-'+i+'"></span></label>'
    + '<input type="text" id="tnam-'+i+'" placeholder="Nombre completo del trámite ' + (i+1) + '" oninput="actualizarMeta();actualizarTabsTramite();syncNombreTramite('+i+')"></div>'
    + '<div class="f" style="flex:1;max-width:380px"><label>Área o dirección responsable</label>'
    + '<input type="text" id="area_resp-'+i+'" placeholder="Unidad interna que gestiona el trámite"></div>'
    + rm
    + '</div>';
}

// Reconstruye las filas de apertura según tramiteCount (sin tope) y sincroniza arrays paralelos.
function actualizarNumTramites(){
  var n = Math.max(1, tramiteCount || 1);
  tramiteCount = n;

  while(flujosActual.length < n) flujosActual.push([]);
  while(flujosPropuesto.length < n) flujosPropuesto.push([]);
  while(reqsTram.length < n) reqsTram.push([]);
  while(accionesTram.length < n) accionesTram.push([]);
  flujosActual.length = n; flujosPropuesto.length = n;
  reqsTram.length = n; accionesTram.length = n;

  var wrap = document.getElementById('tramites-nombres-wrap');
  if(wrap){ wrap.innerHTML = ''; for(var i=0; i<n; i++) wrap.innerHTML += tramRowHTML(i); }

  if(activeTram >= n) activeTram = n-1;
  if(activeTram < 0) activeTram = 0;
  actualizarCodsTramite();
  actualizarTabsTramite();
  renderFichasPanels();
}

// Captura los valores de los trámites desde el DOM (apertura + ficha) para no perderlos al re-render.
function snapshotTramites(){
  var snap = [];
  for(var i=0; i<tramiteCount; i++){
    var o = { tnam: gv('tnam-'+i), area: gv('area_resp-'+i) };
    FICHA_FIELDS.forEach(function(f){ o[f] = gv(f+'_'+i); });
    var al = document.querySelector('input[name="alcance_'+i+'"]:checked');
    o.alcance = al ? al.value : '';
    snap.push(o);
  }
  return snap;
}

function restoreTramites(snap){
  for(var i=0; i<snap.length; i++){
    sv('tnam-'+i, snap[i].tnam || '');
    sv('area_resp-'+i, snap[i].area || '');
    FICHA_FIELDS.forEach(function(f){ if(f!=='tgr_rubro') sv(f+'_'+i, snap[i][f] || ''); });
    onTgrInstChange(i); sv('tgr_rubro_'+i, snap[i].tgr_rubro || '');
    if(snap[i].alcance){
      var al = document.querySelector('input[name="alcance_'+i+'"][value="'+snap[i].alcance+'"]');
      if(al){ al.checked = true; var tp = al.closest('.tp'); if(tp) tp.classList.add('on'); }
    }
    togglePago(i);
  }
  syncAllNombreTramites();
}

function agregarTramiteApertura(){
  var snap = snapshotTramites();
  tramiteCount++;
  actualizarNumTramites();
  restoreTramites(snap);
  renderModeloReqs(activeTram);
  actualizarBVA();
  actualizarMeta();
}

function quitarTramiteApertura(i){
  if(tramiteCount <= 1) return;
  if(!confirm('¿Quitar este trámite y todos sus datos (ficha, requisitos y flujos)?')) return;
  var snap = snapshotTramites();
  snap.splice(i, 1);
  flujosActual.splice(i, 1); flujosPropuesto.splice(i, 1);
  reqsTram.splice(i, 1); accionesTram.splice(i, 1);
  tramiteCount--;
  if(activeTram >= tramiteCount) activeTram = tramiteCount - 1;
  actualizarNumTramites();
  restoreTramites(snap);
  selTram(activeTram);
  actualizarMeta();
}

function getTramNombres(){
  var names = [];
  for(var i=0; i<tramiteCount; i++) names.push(gv('tnam-'+i) || ('Trámite ' + (i+1)));
  return names;
}

function getTramCodigos(){
  var inst = gv('inst');
  var prod = parseInt(gv('num_tramites_prod'))||0;
  var codes = [];
  for(var i=0; i<tramiteCount; i++) codes.push(inst ? (inst + '-' + String(prod+i+1).padStart(2,'0')) : '');
  return codes;
}

function actualizarCodsTramite(){
  var codes = getTramCodigos();
  for(var i=0; i<tramiteCount; i++){
    var el = document.getElementById('tcod-'+i);
    if(el) el.textContent = codes[i];
  }
  actualizarTabsTramite();
}

function migrarDatosTramite(i){
  var sel = document.getElementById('migrar-src-'+i);
  var j = parseInt(sel && sel.value);
  if(isNaN(j) || j < 0 || j >= i) return;
  var codes = getTramCodigos();

  var fieldsToMigrar = ['nombre_corto','modalidad','plazo_legal','tercero',
    'tiempo_real','metodo_pago','pago_banco','pago_cuenta','tgr_inst','tgr_monto',
    'doc_entregado','objetivo','alcance_obs','descripcion','dirigido',
    'horario','telefono','email_tramite','sitio_web'];
  fieldsToMigrar.forEach(function(f){ sv(f+'_'+i, gv(f+'_'+j)); });

  // TGR rubros dependen de la institución — cargar primero, luego asignar rubro
  onTgrInstChange(i);
  sv('tgr_rubro_'+i, gv('tgr_rubro_'+j));

  // Alcance radio + pill styling
  var srcAlc = document.querySelector('input[name="alcance_'+j+'"]:checked');
  document.querySelectorAll('input[name="alcance_'+i+'"]').forEach(function(r){
    r.checked = false;
    r.closest && r.closest('.tp') && r.closest('.tp').classList.remove('on');
  });
  if(srcAlc){
    var dstAlc = document.querySelector('input[name="alcance_'+i+'"][value="'+srcAlc.value+'"]');
    if(dstAlc){ dstAlc.checked = true; dstAlc.closest && dstAlc.closest('.tp') && dstAlc.closest('.tp').classList.add('on'); }
  }

  // Requisitos (copia profunda)
  reqsTram[i] = (reqsTram[j]||[]).map(function(r){ return {requisito:r.requisito||'',obs:r.obs||''}; });
  renderReqFichaRows(i);
  actualizarNumReq();

  // Acciones modelo propuesto (copia profunda)
  accionesTram[i] = (accionesTram[j]||[]).map(function(a){ return {accion:a.accion||'Mantener',justificacion:a.justificacion||''}; });
  renderModeloReqs(i);

  togglePago(i);
  mostrarToast('✓ Datos migrados desde ' + (codes[j] || 'Trámite '+(j+1)));
}

function syncNombreTramite(i){
  var src = document.getElementById('tnam-'+i);
  var dst = document.getElementById('nombre_tramite_'+i);
  if(src && dst) dst.value = src.value;
}

function syncAllNombreTramites(){
  for(var i=0;i<tramiteCount;i++) syncNombreTramite(i);
}

function actualizarTabsTramite(){
  var names = getTramNombres();
  var codes = getTramCodigos();
  var secIds = ['tram-tabs-1','tram-tabs-2','tram-tabs-3','tram-tabs-5'];
  secIds.forEach(function(sid){
    var el = document.getElementById(sid);
    if(!el) return;
    if(tramiteCount <= 1){ el.style.display='none'; el.innerHTML=''; return; }
    el.style.display='flex';
    el.innerHTML = names.map(function(nm,i){
      var label = codes[i] || escHtml(nm.length > 18 ? nm.slice(0,16)+'…' : nm);
      return '<button class="ttab'+(i===activeTram?' active':'')+'" onclick="selTram('+i+')" title="'+escHtml(nm)+'">'
        + label + '</button>';
    }).join('');
  });
}

function selTram(i){
  activeTram = i;
  actualizarTabsTramite();
  mostrarFichaPanel(i);
  renderFlujosActual();
  renderFlujosPropuesto();
  renderModeloReqs(i);
  actualizarBVA();
}

// ── FICHAS POR TRÁMITE ──────────────────────────────────────
function renderFichasPanels(){
  var wrap = document.getElementById('fichas-wrap');
  if(!wrap) return;
  var names = getTramNombres();
  wrap.innerHTML = '';
  for(var i=0; i<tramiteCount; i++){
    var show = (i === activeTram);
    wrap.innerHTML += fichaHTML(i, names[i], show);
  }
  for(var k=0; k<tramiteCount; k++){ renderReqFichaRows(k); togglePago(k); }
  syncAllNombreTramites();
}

function fichaHTML(i, nombre, show){
  var codes = getTramCodigos();
  var names = getTramNombres();
  var codBadge = codes[i] ? ' <span class="tram-cod">'+escHtml(codes[i])+'</span>' : '';

  var migrarHTML = '';
  if(i > 0){
    var opts = '';
    for(var j=0;j<i;j++){
      var lbl = (codes[j]||('Trámite '+(j+1))) + (names[j]?' — '+escHtml(names[j].length>35?names[j].slice(0,33)+'…':names[j]):'');
      opts += '<option value="'+j+'">'+lbl+'</option>';
    }
    migrarHTML = '<div style="display:flex;align-items:center;gap:10px;margin-bottom:1rem;padding:10px 14px;background:var(--azul-c);border-radius:8px;border:1px solid #c8dbff;flex-wrap:wrap">'
      + '<span style="font-size:13px;font-weight:600;color:var(--azul);white-space:nowrap">Migrar datos de:</span>'
      + '<select id="migrar-src-'+i+'" style="font-size:13px;flex:1;min-width:200px;max-width:380px">'+opts+'</select>'
      + '<button class="btn btn-s" onclick="migrarDatosTramite('+i+')" style="font-size:12px;white-space:nowrap">Aplicar</button>'
      + '</div>';
  }

  return '<div id="ficha-panel-'+i+'" style="'+(show?'':'display:none')+'">'
    + migrarHTML
    + '<div class="card accent"><div class="ct">Datos generales — ' + escHtml(nombre) + codBadge + '</div>'
      + '<div class="f"><label>Nombre completo del trámite <span class="star">*</span></label><input type="text" id="nombre_tramite_'+i+'" placeholder="Nombre oficial del trámite"></div>'
      + '<div class="g3">'
        + '<div class="f"><label>Nombre corto / abreviatura</label><input type="text" id="nombre_corto_'+i+'" placeholder="Nombre común"></div>'
        + '<div class="f"><label>Modalidad actual</label><select id="modalidad_'+i+'"><option value="">— Seleccione —</option><option>Presencial</option><option>En línea (parcial)</option><option>En línea (total)</option><option>Mixto</option></select></div>'
        + '<div class="f"><label>Plazo máximo legal</label><input type="text" id="plazo_legal_'+i+'" placeholder="Ej: 15 días hábiles"></div>'
      + '</div>'
      + '<div class="g2">'
        + '<div class="f"><label>Tercero autorizado</label><select id="tercero_'+i+'"><option value="">— Seleccione —</option><option>Sí</option><option>No</option></select></div>'
        + '<div class="f"><label>Documento que se entrega al ciudadano</label><input type="text" id="doc_entregado_'+i+'" placeholder="Ej: Certificado, constancia, licencia"></div>'
      + '</div>'
    + '</div>'
    + '<div class="card"><div class="ct">Tiempos y costos</div>'
      + '<div class="f" style="max-width:340px"><label>Tiempo promedio real (actual)</label><input type="text" id="tiempo_real_'+i+'" placeholder="Ej: 5 días hábiles"></div>'
      + '<div class="f" style="max-width:340px"><label>Método de pago <span class="star">*</span></label>'
        + '<select id="metodo_pago_'+i+'" onchange="togglePago('+i+')"><option value="">— Seleccione —</option><option>Gratuito</option><option>Depósito bancario</option><option>TGR</option></select>'
      + '</div>'
      + '<div id="pago-deposito-'+i+'" style="display:none">'
        + '<div class="g2">'
          + '<div class="f"><label>Banco</label><input type="text" id="pago_banco_'+i+'" placeholder="Nombre del banco"></div>'
          + '<div class="f"><label>Número de cuenta <span class="star">*</span></label><input type="text" id="pago_cuenta_'+i+'" placeholder="No. de cuenta para el depósito"></div>'
        + '</div>'
      + '</div>'
      + '<div id="pago-tgr-'+i+'" style="display:none">'
        + '<div class="g2">'
          + '<div class="f"><label>Institución (TGR)</label><select id="tgr_inst_'+i+'" onchange="onTgrInstChange('+i+')">'+tgrInstOptions()+'</select></div>'
          + '<div class="f"><label>Rubro (TGR)</label><select id="tgr_rubro_'+i+'">'+tgrRubroOptionsFor('')+'</select></div>'
        + '</div>'
        + '<div class="g2">'
          + '<div class="f"><label>Monto específico (Lps.) <span class="star">*</span></label><input type="number" id="tgr_monto_'+i+'" placeholder="Valor que la institución cobra por este trámite" min="0" step="0.01"></div>'
          + '<div class="f" style="display:flex;align-items:flex-end"><a href="https://tgr1.sefin.gob.hn/tgr/tgr1/" target="_blank" rel="noopener" style="font-size:12px;color:var(--azul-m);font-weight:600;text-decoration:none">Consultar catálogo en TGR-1 ↗</a></div>'
        + '</div>'
      + '</div>'
    + '</div>'
    + '<div class="card"><div class="ct">Objetivo y alcance</div>'
      + '<div class="f"><label>Objetivo del trámite</label><textarea id="objetivo_'+i+'" rows="4" placeholder="Descripción del objetivo del trámite según lo indicado por el líder durante el levantamiento..."></textarea></div>'
      + '<div class="f"><label>Alcance</label>'
        + '<div class="tg">'
          + '<label class="tp"><input type="radio" name="alcance_'+i+'" value="Nacional"> Nacional</label>'
          + '<label class="tp"><input type="radio" name="alcance_'+i+'" value="Regional"> Regional</label>'
          + '<label class="tp"><input type="radio" name="alcance_'+i+'" value="Municipal"> Municipal</label>'
        + '</div>'
      + '</div>'
      + '<div class="f"><label>Observaciones sobre el alcance</label><input type="text" id="alcance_obs_'+i+'" placeholder="Detalles adicionales sobre cobertura geográfica o institucional..."></div>'
    + '</div>'
    + '<div class="card"><div class="ct">Requisitos del trámite</div>'
      + '<p style="font-size:12px;color:var(--muted);margin-bottom:.9rem">Registre cada requisito del trámite. Estos se traerán automáticamente al <strong>Modelo propuesto</strong> para definir su acción.</p>'
      + '<table class="dtbl"><thead><tr><th>#</th><th>Requisito / descripción</th><th>Observación</th><th style="width:36px"></th></tr></thead>'
        + '<tbody id="req-ficha-tbody-'+i+'"></tbody></table>'
      + '<button class="btn-add" onclick="addReqFicha('+i+')">+ Agregar requisito</button>'
    + '</div>'
    + '<div class="card"><div class="ct">Descripción y contacto</div>'
      + '<div class="f"><label>Descripción del servicio</label><textarea id="descripcion_'+i+'" rows="3" placeholder="¿Qué hace este trámite? ¿A quién sirve?"></textarea></div>'
      + '<div class="g2">'
        + '<div class="f"><label>Dirigido a</label><input type="text" id="dirigido_'+i+'" placeholder="Ciudadanos, empresas, instituciones..."></div>'
        + '<div class="f"><label>Horario de atención</label><input type="text" id="horario_'+i+'" placeholder="Ej: Lun-Vie 8:00-16:00"></div>'
      + '</div>'
      + '<div class="g3">'
        + '<div class="f"><label>Teléfono</label><input type="text" id="telefono_'+i+'" placeholder="+504 0000-0000"></div>'
        + '<div class="f"><label>Correo del trámite</label><input type="email" id="email_tramite_'+i+'" placeholder="correo@inst.gob.hn"></div>'
        + '<div class="f"><label>Sitio web</label><input type="text" id="sitio_web_'+i+'" placeholder="https://..."></div>'
      + '</div>'
    + '</div>'
  + '</div>';
}

function mostrarFichaPanel(i){
  for(var j=0; j<tramiteCount; j++){
    var p = document.getElementById('ficha-panel-'+j);
    if(p) p.style.display = j===i ? '' : 'none';
  }
}

// ── FLOW BUILDER ────────────────────────────────────────────
var TIPO_ICONS = {inicio:'▶', paso:'◉', decision:'⬦', fin:'■'};
var TIPO_LABELS = {inicio:'INICIO', paso:'PASO', decision:'DECISIÓN', fin:'FIN'};

function renderFlujosActual(){
  renderFlujoList('flujo-actual-list', flujosActual[activeTram] || []);
}
function renderFlujosPropuesto(){
  renderFlujoList('flujo-propuesto-list', flujosPropuesto[activeTram] || []);
}

function renderFlujoList(listId, data){
  var list = document.getElementById(listId);
  if(!list) return;
  if(!data || data.length === 0){
    list.innerHTML = '<div style="text-align:center;padding:1.5rem;color:var(--muted);font-size:13px;background:var(--gris-f);border-radius:10px;border:1.5px dashed var(--borde)">Sin pasos aún. Use los botones de abajo para comenzar el flujo.</div>';
    return;
  }
  list.innerHTML = data.map(function(step, idx){
    return buildNodeHTML(listId, step, idx, data);
  }).join('');
}

function buildNodeHTML(listId, step, idx, allSteps){
  var tipo = step.tipo || 'paso';
  var retOpts = '<option value="">— Sin retorno —</option>';
  for(var j=0; j<idx; j++){
    var st = allSteps[j];
    retOpts += '<option value="'+j+'"'+(step.retorno_a===j?' selected':'')+'>Paso '+(j+1)+' — '+escHtml((st.titulo||'').slice(0,30)||'Sin nombre')+'</option>';
  }
  var hasRet = step.retorno_a !== null && step.retorno_a !== undefined && step.retorno_a !== '';
  var isActual = listId === 'flujo-actual-list';
  var fnKey = isActual ? 'actual' : 'propuesto';

  return '<div class="flow-node fn-'+tipo+'" id="fn-'+listId+'-'+idx+'">'
    + '<div class="flow-num">'+TIPO_ICONS[tipo]+'</div>'
    + '<div class="flow-body">'
      + '<div class="fn-hdr">'
        + '<select class="fn-tipo-sel" onchange="updateFlowTipo(\''+fnKey+'\','+idx+',this.value)" title="Tipo de nodo">'
          + ['inicio','paso','decision','fin'].map(function(t){ return '<option value="'+t+'"'+(tipo===t?' selected':'')+'>'+TIPO_LABELS[t]+'</option>'; }).join('')
        + '</select>'
        + '<input class="fn-titulo-inp" placeholder="Nombre de la actividad" value="'+escHtml(step.titulo||'')+'" onchange="updateFlowField(\''+fnKey+'\','+idx+',\'titulo\',this.value)">'
        + '<div class="fn-acts">'
          + (idx>0 ? '<button onclick="moveFlowNode(\''+fnKey+'\','+idx+',-1)" title="Subir">↑</button>' : '')
          + (idx<allSteps.length-1 ? '<button onclick="moveFlowNode(\''+fnKey+'\','+idx+',1)" title="Bajar">↓</button>' : '')
          + '<button class="fn-del" onclick="removeFlowNode(\''+fnKey+'\','+idx+')" title="Eliminar">✕</button>'
        + '</div>'
      + '</div>'
      + '<div class="fn-fields">'
        + '<input placeholder="Área responsable" value="'+escHtml(step.area||'')+'" onchange="updateFlowField(\''+fnKey+'\','+idx+',\'area\',this.value)">'
        + '<input placeholder="Tiempo observado" value="'+escHtml(step.tiempo||'')+'" onchange="updateFlowField(\''+fnKey+'\','+idx+',\'tiempo\',this.value)">'
        + '<input placeholder="Documento emitido (si aplica)" value="'+escHtml(step.doc_emitido||'')+'" onchange="updateFlowField(\''+fnKey+'\','+idx+',\'doc_emitido\',this.value)">'
      + '</div>'
      + '<textarea class="fn-obs" placeholder="Observación de campo..." onchange="updateFlowField(\''+fnKey+'\','+idx+',\'obs\',this.value)">'+escHtml(step.obs||'')+'</textarea>'
      + (hasRet ? '<div class="ret-badge" style="display:inline-flex;margin-top:6px">↩ Retorna al Paso '+(step.retorno_a+1)+'</div>' : '')
      + (idx>0 ? '<div class="fn-ret"><span>↩ Retorno:</span><select onchange="updateFlowField(\''+fnKey+'\','+idx+',\'retorno_a\',this.value===\'\'?null:parseInt(this.value))">'+retOpts+'</select></div>' : '')
    + '</div>'
  + '</div>';
}

function addFlowNode(key, tipo){
  var arr = key==='actual' ? flujosActual[activeTram] : flujosPropuesto[activeTram];
  if(!arr){ if(key==='actual') flujosActual[activeTram]=[]; else flujosPropuesto[activeTram]=[]; arr=key==='actual'?flujosActual[activeTram]:flujosPropuesto[activeTram]; }
  arr.push({tipo:tipo||'paso', titulo:'', area:'', tiempo:'', doc_emitido:'', obs:'', retorno_a:null});
  if(key==='actual') renderFlujosActual(); else renderFlujosPropuesto();
  actualizarBVA();
}

function removeFlowNode(key, idx){
  var arr = key==='actual' ? flujosActual[activeTram] : flujosPropuesto[activeTram];
  arr.splice(idx, 1);
  // Fix retorno_a references
  arr.forEach(function(s){ if(s.retorno_a !== null && s.retorno_a >= idx) s.retorno_a = s.retorno_a > idx ? s.retorno_a-1 : null; });
  if(key==='actual') renderFlujosActual(); else renderFlujosPropuesto();
  actualizarBVA();
}

function moveFlowNode(key, idx, dir){
  var arr = key==='actual' ? flujosActual[activeTram] : flujosPropuesto[activeTram];
  var to = idx + dir;
  if(to < 0 || to >= arr.length) return;
  var tmp = arr[idx]; arr[idx] = arr[to]; arr[to] = tmp;
  if(key==='actual') renderFlujosActual(); else renderFlujosPropuesto();
}

function updateFlowTipo(key, idx, val){
  var arr = key==='actual' ? flujosActual[activeTram] : flujosPropuesto[activeTram];
  arr[idx].tipo = val;
  if(key==='actual') renderFlujosActual(); else renderFlujosPropuesto();
}

function updateFlowField(key, idx, field, val){
  var arr = key==='actual' ? flujosActual[activeTram] : flujosPropuesto[activeTram];
  arr[idx][field] = val;
  // Only re-render retorno badges, not full list (avoid losing focus)
  if(field === 'retorno_a'){
    if(key==='actual') renderFlujosActual(); else renderFlujosPropuesto();
  }
}

// ── BEFORE VS AFTER ─────────────────────────────────────────
function actualizarBVA(){
  var funcAntes = parseInt(gv('num_func')) || 0;
  var funcProp  = parseInt(gv('func_dig')) || 0;
  var pasosAnt  = (flujosActual[activeTram] || []).length;
  var pasosProp = (flujosPropuesto[activeTram] || []).length;
  var tiempoAnt = gv('tiempo_obs') || '—';
  var tiempoProp= gv('tiempo_dig') || '—';

  document.getElementById('bva-func-antes').textContent = funcAntes || '—';
  document.getElementById('bva-pasos-antes').textContent = pasosAnt || '—';
  document.getElementById('bva-tiempo-antes').textContent = tiempoAnt;
  document.getElementById('bva-func-prop').textContent = funcProp || '—';
  document.getElementById('bva-pasos-prop').textContent = pasosProp || '—';
  document.getElementById('bva-tiempo-prop').textContent = tiempoProp;

  // Calcular % reducción de pasos
  if(pasosAnt > 0 && pasosProp > 0){
    var redPasos = Math.round(((pasosAnt - pasosProp) / pasosAnt) * 100);
    var pctEl = document.getElementById('bva-pct-val');
    pctEl.textContent = (redPasos >= 0 ? '-' : '+') + Math.abs(redPasos) + '%';
    pctEl.className = 'bva-pct' + (redPasos < 0 ? ' bva-pct-neg' : '');
    document.getElementById('bva-pct-lbl').textContent = redPasos >= 0 ? 'reducción en pasos' : 'aumento en pasos';
  } else {
    document.getElementById('bva-pct-val').textContent = '—';
    document.getElementById('bva-pct-lbl').textContent = 'reducción estimada';
  }
}

// ── RESUMEN REQUISITOS ───────────────────────────────────────
// Renderiza los requisitos traídos de la ficha del trámite activo, con acción + justificación
function renderModeloReqs(i){
  if(i===undefined) i = activeTram;
  var wrap = document.getElementById('modelo-reqs-wrap');
  if(!wrap) return;
  var reqs = reqsTram[i] || [];
  if(!accionesTram[i]) accionesTram[i] = [];
  var reales = reqs.filter(function(r){ return (r.requisito||'').trim(); });
  if(reales.length === 0){
    wrap.innerHTML = '<p style="font-size:12.5px;color:var(--muted)">No hay requisitos registrados. Agréguelos en la <strong>Ficha del trámite → Requisitos del trámite</strong>.</p>';
    renderReqResumen();
    return;
  }
  var ACC = ['Eliminar','Simplificar','Digitalizar','Mantener'];
  var rows = reqs.map(function(r, idx){
    if(!(r.requisito||'').trim()) return '';
    var a = accionesTram[i][idx] || (accionesTram[i][idx]={accion:'Mantener',justificacion:''});
    return '<div style="display:grid;grid-template-columns:1fr 150px 1.3fr;gap:10px;align-items:start;padding:.6rem 0;border-bottom:1px solid #f0f2f7">'
      + '<div style="font-size:12.5px;color:var(--texto);padding-top:7px"><strong>'+escHtml(r.requisito)+'</strong>'+(r.obs?'<div style="font-size:11px;color:var(--muted);margin-top:2px">'+escHtml(r.obs)+'</div>':'')+'</div>'
      + '<select onchange="updateAccion('+i+','+idx+',\'accion\',this.value);renderReqResumen()" style="padding:7px 9px;border:1.5px solid var(--borde);border-radius:7px;font-size:12.5px;font-family:inherit">'
        + ACC.map(function(o){ return '<option'+(a.accion===o?' selected':'')+'>'+o+'</option>'; }).join('') + '</select>'
      + '<textarea placeholder="Justificación..." onchange="updateAccion('+i+','+idx+',\'justificacion\',this.value)" style="padding:7px 9px;border:1.5px solid var(--borde);border-radius:7px;font-size:12.5px;font-family:inherit;min-height:40px;resize:vertical">'+escHtml(a.justificacion||'')+'</textarea>'
    + '</div>';
  }).join('');
  wrap.innerHTML = '<div style="display:grid;grid-template-columns:1fr 150px 1.3fr;gap:10px;font-size:10px;font-weight:800;color:var(--muted);text-transform:uppercase;letter-spacing:.05em;padding-bottom:6px;border-bottom:2px solid var(--azul-c)"><span>Requisito</span><span>Acción</span><span>Justificación</span></div>' + rows;
  renderReqResumen();
}
function updateAccion(i,idx,field,val){
  if(!accionesTram[i]) accionesTram[i]=[];
  if(!accionesTram[i][idx]) accionesTram[i][idx]={accion:'Mantener',justificacion:''};
  accionesTram[i][idx][field]=val;
}
function renderReqResumen(){
  var wrap = document.getElementById('req-resumen-wrap');
  if(!wrap) return;
  var i = activeTram;
  var reqs = reqsTram[i] || [];
  var grupos = {eliminar:[], simplificar:[], digitalizar:[], mantener:[]};
  reqs.forEach(function(r, idx){
    var nombre = (r.requisito||'').trim();
    if(!nombre) return;
    var a = (accionesTram[i] && accionesTram[i][idx]) ? accionesTram[i][idx].accion : 'Mantener';
    var key = (a||'Mantener').toLowerCase().normalize('NFD').replace(/[̀-ͯ]/g,'');
    key = ['eliminar','simplificar','digitalizar'].indexOf(key)>=0 ? key : 'mantener';
    grupos[key].push(nombre);
  });
  var orden = ['eliminar','simplificar','digitalizar','mantener'];
  var labels = {eliminar:'Eliminar',simplificar:'Simplificar',digitalizar:'Digitalizar',mantener:'Mantener'};
  var html = '';
  orden.forEach(function(k){
    grupos[k].forEach(function(nombre){
      html += '<div class="req-sum-item '+k+'"><span class="req-sum-badge">'+labels[k]+'</span><span>'+escHtml(nombre)+'</span></div>';
    });
  });
  wrap.innerHTML = html || '<p style="font-size:12.5px;color:var(--muted)">Registre requisitos en la ficha del trámite para ver el resumen aquí.</p>';
}

// ── TABLES DINÁMICAS ─────────────────────────────────────────
function agregarLegal(){
  var tbody = document.getElementById('legal-tbody');
  var n = tbody.rows.length + 1;
  var tr = tbody.insertRow();
  tr.innerHTML = '<td class="nc">'+n+'</td>'
    +'<td><input type="text" placeholder="Ej: Ley de Procedimiento Administrativo"></td>'
    +'<td><input type="text" placeholder="Arts. 12, 15"></td>'
    +'<td><textarea placeholder="Observación..."></textarea></td>'
    +'<td style="text-align:center;padding:6px"><button class="btn-rm" onclick="rmRow(this)">✕</button></td>';
}

function agregarDocSolicitado(){
  var tbody = document.getElementById('docs-tbody');
  var n = tbody.rows.length + 1;
  var tr = tbody.insertRow();
  tr.innerHTML = '<td class="nc">'+n+'</td>'
    +'<td><input type="text" placeholder="Nombre del documento"></td>'
    +'<td><select style="padding:8px 10px;border:none;background:transparent;font-size:12.5px;font-family:inherit"><option>Flujo de proceso</option><option>Marco legal</option><option>Manual interno</option><option>Formulario</option><option>Instructivo</option><option>Organigrama</option><option>Otro</option></select></td>'
    +'<td style="text-align:center"><select style="padding:8px 10px;border:none;background:transparent;font-size:12.5px;font-family:inherit"><option>Pendiente</option><option>Recibido</option><option>No disponible</option></select></td>'
    +'<td><input type="date"></td>'
    +'<td><input type="text" placeholder="URL de Drive o nombre del .zip" style="font-size:12px"></td>'
    +'<td style="text-align:center;padding:6px"><button class="btn-rm" onclick="rmRow(this)">✕</button></td>';
}

function agregarDocInterno(){
  var tbody = document.getElementById('docint-tbody');
  if(!tbody) return;
  var n = tbody.rows.length + 1;
  var tr = tbody.insertRow();
  tr.innerHTML = '<td class="nc">'+n+'</td>'
    +'<td><input type="text" placeholder="Nombre del documento interno"></td>'
    +'<td><input type="text" placeholder="Área / unidad"></td>'
    +'<td><textarea placeholder="Observación..."></textarea></td>'
    +'<td style="text-align:center;padding:6px"><button class="btn-rm" onclick="rmRow(this)">✕</button></td>';
}

function rmRow(btn){ btn.closest('tr').remove(); }

// ── ESTADOS ──────────────────────────────────────────────────
function actualizarEstados(){
  var total=5, completadas=0;
  for(var i=0;i<6;i++){
    var el = document.getElementById('est-'+i);
    if(el && (el.value==='Completo'||el.value==='Validado')) completadas++;
    var sb = document.getElementById('sb'+i);
    if(sb){
      if(el && (el.value==='Completo'||el.value==='Validado')) sb.classList.add('done'); else sb.classList.remove('done');
    }
  }
  var pct = Math.round((completadas/total)*100);
  document.getElementById('pb-fill').style.width = pct+'%';
  document.getElementById('pb-pct').textContent = pct+'%';
}

// ── PILLS ────────────────────────────────────────────────────
document.addEventListener('DOMContentLoaded', async function(){
  document.addEventListener('click', function(e){
    var pill = e.target.closest('.tp');
    if(!pill) return;
    var tg = pill.closest('.tg');
    if(!tg) return;
    var inp = pill.querySelector('input');
    if(!inp || !inp.name) return;
    tg.querySelectorAll('.tp').forEach(function(p){ p.classList.remove('on','on-v'); });
    pill.classList.add('on');
  });
  // Inicialización (datos inyectados por el portal)
  renderFichasPanels();
  renderInfraAll();
  renderFlujosActual();
  renderFlujosPropuesto();
  renderModeloReqs(0);
  actualizarEstados();
  cargarContactosAsistencias();

  var meta = window.__EXPMETA__ || {};
  if(window.__EXP__){
    currentIdx = meta.id || 0;
    poblarFormulario(window.__EXP__);
  } else {
    nuevoExp();
  }
  ir(0);
});

// ── GUARDAR / CARGAR ─────────────────────────────────────────
function recolectar(){
  var d = {};

  // Apertura
  ['inst','fecha_apertura','analista','codigo_exp','dir_sede','contacto_nombre','contacto_cargo',
   'contacto_correo','contacto_tel'].forEach(function(id){ d[id]=gv(id); });
  d.num_tramites = tramiteCount;
  d.num_tramites_prod = parseInt(gv('num_tramites_prod'))||0;
  d.tramite_nombres = [];
  d.tramite_areas = [];
  for(var i=0;i<tramiteCount;i++){ d.tramite_nombres.push(gv('tnam-'+i)); d.tramite_areas.push(gv('area_resp-'+i)); }

  // Tramites
  d.tramites = [];
  var fichaFields = ['nombre_tramite','nombre_corto','modalidad','plazo_legal','tercero',
    'tiempo_real','metodo_pago','pago_banco','pago_cuenta','tgr_inst','tgr_rubro','tgr_monto',
    'doc_entregado','objetivo','alcance_obs','descripcion','dirigido',
    'horario','telefono','email_tramite','sitio_web'];
  for(var t=0;t<tramiteCount;t++){
    var ft = {};
    fichaFields.forEach(function(f){ ft[f] = gv(f+'_'+t); });
    var alc = document.querySelector('input[name="alcance_'+t+'"]:checked');
    ft.alcance = alc ? alc.value : '';
    d.tramites.push(ft);
  }

  // Requisitos por trámite + acciones del modelo
  d.reqs_tram = reqsTram.slice(0, tramiteCount);
  d.acciones_tram = accionesTram.slice(0, tramiteCount);

  // Infraestructura SOL (una vez por expediente)
  d.infra = recolectarInfra();

  // Secciones compartidas (sólo trámite activo para tablas y flujos)
  d.legal   = tblData('legal-tbody',  ['instrumento','articulos','obs']);
  d.docs    = tblData('docs-tbody',   ['nombre','tipo','recibido','fecha','url']);
  d.obs_legal = gv('obs_legal');
  d.num_func  = gv('num_func');
  d.num_req   = gv('num_req');
  d.num_anio  = gv('num_anio');
  d.tiempo_obs = gv('tiempo_obs');
  var radTn = document.querySelector('input[name="t_norm"]:checked');
  d.t_norm = radTn ? radTn.value : '';
  d.desc_proceso = gv('desc_proceso');
  d.docs_internos = tblData('docint-tbody', ['documento','area','obs']);
  d.docs_add = gv('docs_add');
  d.obs_flujo = gv('obs_flujo');
  d.func_dig  = gv('func_dig');
  d.tiempo_dig = gv('tiempo_dig');
  d.obs_modelo = gv('obs_modelo');

  // Flows (todos los trámites)
  d.flujos_actual    = flujosActual.slice(0, tramiteCount);
  d.flujos_propuesto = flujosPropuesto.slice(0, tramiteCount);

  // Reuniones
  d.reuniones = [];
  document.querySelectorAll('.reunion-card').forEach(function(card){
    var n = card.id.replace('reunion-','');
    var acuerdos = [];
    card.querySelectorAll('.acuerdo-item').forEach(function(item){
      var inps = item.querySelectorAll('input');
      acuerdos.push({desc:inps[0]?inps[0].value:'',responsable:inps[1]?inps[1].value:'',fecha:inps[2]?inps[2].value:''});
    });
    d.reuniones.push({fecha:gv('reu-fecha-'+n),tipo:gv('reu-tipo-'+n),lugar:gv('reu-lugar-'+n),participantes:gv('reu-part-'+n),temas:gv('reu-temas-'+n),acuerdos:acuerdos});
  });

  // Estado
  d.estados={}; d.notas={};
  for(var j=0;j<6;j++){
    d.estados[j]=(document.getElementById('est-'+j)||{}).value||'Pendiente';
    d.notas[j]=(document.getElementById('nota-'+j)||{}).value||'';
  }
  var radSt = document.querySelector('input[name="estado_exp"]:checked');
  d.estado_exp = radSt ? radSt.value : '';
  var radLev = document.querySelector('input[name="estado_lev"]:checked');
  d.estado_lev = radLev ? radLev.value : '';
  d.obs_levantamiento = gv('obs_levantamiento');
  ['obs_expediente','validado_diger','validado_inst','fecha_validacion','num_acta'].forEach(function(id){ d[id]=gv(id); });

  d._ts = new Date().toISOString();
  return d;
}

function tblData(tbodyId, cols){
  var rows=[], tbody=document.getElementById(tbodyId);
  if(!tbody) return rows;
  Array.from(tbody.rows).forEach(function(tr){
    var inputs=tr.querySelectorAll('input,select,textarea');
    var obj={};
    cols.forEach(function(col,i){ obj[col]=inputs[i]?inputs[i].value:''; });
    rows.push(obj);
  });
  return rows;
}

async function guardar(){
  var meta = window.__EXPMETA__ || {};
  if(!gv('inst')){ mostrarToast('Seleccione la institución'); ir(0); return; }
  if(!gv('analista')){ mostrarToast('Indique el analista responsable'); ir(0); return; }
  var btn = document.querySelector('.btn-save-float');
  if(btn){ btn.disabled=true; btn.textContent='Guardando…'; }
  try{
    var data = recolectar();
    var resp = await fetch(meta.postUrl || window.location.href, {
      method:'POST',
      headers:{ 'Content-Type':'application/json', 'RequestVerificationToken': meta.token || '' },
      body: JSON.stringify(data)
    });
    if(!resp.ok){ throw new Error('HTTP '+resp.status); }
    var result = await resp.json();
    mostrarToast('✓ Expediente guardado');
    setTimeout(function(){ window.location.href = (meta.indexUrl || '/'); }, 600);
  }catch(err){
    if(btn){ btn.disabled=false; btn.innerHTML='💾 Guardar'; }
    mostrarToast('✗ Error al guardar: ' + err.message);
  }
}

function cargarTodos(){ return expCache; }

function poblarFormulario(d){
  // Apertura
  ['inst','fecha_apertura','analista','codigo_exp','dir_sede','contacto_nombre','contacto_cargo',
   'contacto_correo','contacto_tel'].forEach(function(id){ sv(id, d[id]||''); });
  filtrarContactosPorInstitucion();

  // Requisitos/acciones por trámite (antes de render para que aparezcan)
  reqsTram = d.reqs_tram ? d.reqs_tram.map(function(r){ return r||[]; }) : [[]];
  accionesTram = d.acciones_tram ? d.acciones_tram.map(function(a){ return a||[]; }) : [[]];

  // Tramites count + nombres + áreas
  tramiteCount = d.num_tramites || 1;
  sv('num_tramites', tramiteCount);
  sv('num_tramites_prod', d.num_tramites_prod||0);
  actualizarNumTramites();
  if(d.tramite_nombres) d.tramite_nombres.forEach(function(nm,i){ sv('tnam-'+i, nm||''); });
  if(d.tramite_areas) d.tramite_areas.forEach(function(ar,i){ sv('area_resp-'+i, ar||''); });
  while(reqsTram.length < tramiteCount) reqsTram.push([]);
  while(accionesTram.length < tramiteCount) accionesTram.push([]);
  actualizarTabsTramite();
  renderFichasPanels();

  // Ficha fields
  var fichaFields = ['nombre_tramite','nombre_corto','modalidad','plazo_legal','tercero',
    'tiempo_real','metodo_pago','pago_banco','pago_cuenta','tgr_inst','tgr_rubro','tgr_monto',
    'doc_entregado','objetivo','alcance_obs','descripcion','dirigido',
    'horario','telefono','email_tramite','sitio_web'];
  if(d.tramites) d.tramites.forEach(function(ft,t){
    fichaFields.forEach(function(f){
      if(f === 'nombre_tramite') return; // siempre derivado de tnam en apertura
      sv(f+'_'+t, ft[f]||'');
    });
    onTgrInstChange(t); sv('tgr_rubro_'+t, ft.tgr_rubro||'');
    if(ft.alcance){ var al=document.querySelector('input[name="alcance_'+t+'"][value="'+ft.alcance+'"]'); if(al) al.checked=true; }
    togglePago(t);
  });
  syncAllNombreTramites();

  // Infraestructura SOL
  poblarInfra(d.infra);

  // Legal/docs
  recargarTabla('legal-tbody', d.legal||[], agregarLegal, ['instrumento','articulos','obs']);
  recargarTabla('docs-tbody', d.docs||[], agregarDocSolicitado, ['nombre','tipo','recibido','fecha','url']);
  sv('obs_legal', d.obs_legal||'');

  // Proceso
  ['num_func','num_anio','tiempo_obs','desc_proceso','docs_add','obs_flujo','func_dig','tiempo_dig','obs_modelo'].forEach(function(id){ sv(id, d[id]||''); });
  if(d.t_norm){ var tn=document.querySelector('input[name="t_norm"][value="'+d.t_norm+'"]'); if(tn) tn.checked=true; }
  recargarTabla('docint-tbody', d.docs_internos||[], agregarDocInterno, ['documento','area','obs']);

  // Flows
  flujosActual = d.flujos_actual ? d.flujos_actual.map(function(f){ return f||[]; }) : [[]];
  flujosPropuesto = d.flujos_propuesto ? d.flujos_propuesto.map(function(f){ return f||[]; }) : [[]];
  while(flujosActual.length < tramiteCount) flujosActual.push([]);
  while(flujosPropuesto.length < tramiteCount) flujosPropuesto.push([]);
  activeTram = 0;
  actualizarCodsTramite();
  renderFlujosActual();
  renderFlujosPropuesto();
  renderModeloReqs(0);
  actualizarBVA();

  // Reuniones (sección opcional — sólo si existe en el DOM)
  var _rw=document.getElementById('reuniones-wrap');
  if(_rw && typeof agregarReunion==='function'){ _rw.innerHTML=''; reunionCount=0;
  if(d.reuniones) d.reuniones.forEach(function(r){
    agregarReunion();
    var n=reunionCount;
    sv('reu-fecha-'+n,r.fecha||''); sv('reu-tipo-'+n,r.tipo||'');
    sv('reu-lugar-'+n,r.lugar||''); sv('reu-part-'+n,r.participantes||'');
    sv('reu-temas-'+n,r.temas||'');
    var aw=document.getElementById('acuerdos-'+n); aw.innerHTML='';
    if(r.acuerdos) r.acuerdos.forEach(function(a){
      agregarAcuerdo(n);
      var inps=aw.lastElementChild.querySelectorAll('input');
      if(inps[0]) inps[0].value=a.desc||'';
      if(inps[1]) inps[1].value=a.responsable||'';
      if(inps[2]) inps[2].value=a.fecha||'';
    });
  });
  }

  // Estado
  if(d.estados) for(var j=0;j<6;j++){
    sv('est-'+j, d.estados[j]||'Pendiente');
    sv('nota-'+j, d.notas&&d.notas[j]?d.notas[j]:'');
  }
  if(d.estado_exp){
    var el=document.querySelector('input[name="estado_exp"][value="'+d.estado_exp+'"]');
    if(el){ el.checked=true; el.closest('.tp')&&el.closest('.tp').classList.add('on-v'); }
  }
  if(d.estado_lev){
    var elv=document.querySelector('input[name="estado_lev"][value="'+d.estado_lev+'"]');
    if(elv){ elv.checked=true; elv.closest('.tp')&&elv.closest('.tp').classList.add('on'); }
  }
  sv('obs_levantamiento', d.obs_levantamiento||'');
  ['obs_expediente','validado_diger','validado_inst','fecha_validacion','num_acta'].forEach(function(id){ sv(id, d[id]||''); });

  actualizarMeta();
  actualizarEstados();
  actualizarNumReq();
}

function recargarTabla(tbodyId, data, addFn, cols){
  var tbody=document.getElementById(tbodyId);
  if(!tbody) return;
  tbody.innerHTML='';
  data.forEach(function(row){
    addFn();
    var tr=tbody.rows[tbody.rows.length-1];
    var inputs=tr.querySelectorAll('input,select,textarea');
    cols.forEach(function(col,i){ if(inputs[i]) inputs[i].value=row[col]||''; });
  });
}

// ── HISTORIAL ────────────────────────────────────────────────
function verHistorial(){
  document.getElementById('hist-view').style.display='block';
  document.getElementById('form-view').style.display='none';
  document.getElementById('btn-volver').style.display='none';
  document.querySelector('.btn-save-float').style.display='none';
  var all=cargarTodos();
  var lista=document.getElementById('hist-lista');
  if(all.length===0){
    lista.innerHTML='<div class="hist-empty"><div class="ico">📁</div><p>No hay expedientes registrados.<br>Crea el primero con el botón de arriba.</p></div>';
  } else {
    lista.innerHTML=all.map(function(exp,i){
      var ts=exp._ts?new Date(exp._ts).toLocaleDateString('es-HN'):'—';
      var prod = exp.num_tramites_prod||0;
      var names = (exp.tramite_nombres||[]).filter(Boolean);
      var tramBadges = names.map(function(nm,idx){
        var col = TRAM_COLORS[idx % TRAM_COLORS.length];
        var cod = exp.inst ? (exp.inst+'-'+String(prod+idx+1).padStart(2,'0')) : '';
        return '<span title="'+escHtml(nm)+'" style="display:inline-flex;align-items:center;font-size:11px;font-weight:700;'
          +'background:'+col.bg+';color:'+col.fg+';border-radius:4px;padding:2px 8px;margin:2px 3px 2px 0;white-space:nowrap">'
          +escHtml(cod||nm)+'</span>';
      }).join('');
      if(!tramBadges) tramBadges='<span style="color:var(--muted);font-size:12px">Sin trámites</span>';
      return '<div class="hist-card" onclick="cargarExp('+i+')">'
        +'<div class="hist-card-av">'+(exp.inst||'?').slice(0,2).toUpperCase()+'</div>'
        +'<div class="hist-card-info">'
          +'<div class="hist-card-title">'+(exp.inst||'Sin institución')+'</div>'
          +'<div class="hist-card-meta" style="white-space:normal;line-height:1.6">'+tramBadges+'</div>'
          +'<div class="hist-card-meta">'+escHtml(exp.codigo_exp||'')+(exp.codigo_exp?' · ':'')+'Guardado: '+ts+'</div>'
        +'</div>'
        +'<button class="hist-card-del" onclick="event.stopPropagation();eliminarExp('+i+')">✕</button>'
      +'</div>';
    }).join('');
  }
}

function mostrarFormView(){
  document.getElementById('hist-view').style.display='none';
  document.getElementById('form-view').style.display='block';
  document.getElementById('btn-volver').style.display='';
  document.querySelector('.btn-save-float').style.display='flex';
}

function cargarExp(i){
  currentIdx=i;
  poblarFormulario(cargarTodos()[i]);
  mostrarFormView(); ir(0);
}

function iniciarNuevoExp(){ nuevoExp(); mostrarFormView(); }

function eliminarExp(){ /* el historial se gestiona en la página de inicio del portal */ }

function nuevoExp(){
  currentIdx=null; tramiteCount=1; activeTram=0;
  flujosActual=[[]]; flujosPropuesto=[[]];
  reqsTram=[[]]; accionesTram=[[]];
  document.querySelectorAll('input:not([type=radio]):not([type=checkbox]),select,textarea').forEach(function(el){ if(el.id&&!el.readOnly) el.value=''; });
  document.querySelectorAll('input[type=checkbox]').forEach(function(el){ el.checked=false; });
  document.querySelectorAll('input[type=radio]').forEach(function(el){ el.checked=false; });
  document.querySelectorAll('.tg .tp').forEach(function(p){ p.classList.remove('on','on-v'); });
  document.querySelectorAll('[id^="est-"]').forEach(function(el){ el.value='Pendiente'; });
  ['legal-tbody','docs-tbody','docint-tbody'].forEach(function(id){ var el=document.getElementById(id); if(el) el.innerHTML=''; });
  sv('num_tramites_prod','0');
  actualizarNumTramites(); // construye la fila del trámite 1 dinámicamente
  var cbs = document.getElementById('contacto-buscar');
  if(cbs) cbs.value = '';
  renderFichasPanels();
  renderFlujosActual();
  renderFlujosPropuesto();
  renderModeloReqs(0);
  renderInfraAll();
  ['tram-tabs-1','tram-tabs-2','tram-tabs-3','tram-tabs-5'].forEach(function(id){ var el=document.getElementById(id); if(el){el.style.display='none';el.innerHTML='';} });
  actualizarMeta(); actualizarEstados();
}

function cerrarHistorial(){ verHistorial(); }


// ── UTILS ────────────────────────────────────────────────────
function gv(id){ var el=document.getElementById(id); return el?(el.value||'').trim():''; }
function sv(id,val){ var el=document.getElementById(id); if(el) el.value=val; }
function escHtml(s){ return String(s||'').replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;'); }
function mostrarToast(msg){
  var t=document.getElementById('toast'); t.textContent=msg;
  t.classList.add('show'); setTimeout(function(){ t.classList.remove('show'); },2500);
}

// (autoguardado deshabilitado; el guardado es explícito vía el botón Guardar)

// ── FLOW DIAGRAM VIEWER ──────────────────────────────────────
var diagGAP={actual:30,propuesto:30};

function adjustDiagGAP(key,delta){
  diagGAP[key]=Math.max(10,Math.min(120,(diagGAP[key]||30)+delta));
  refreshDiagIfVisible(key);
}

function buildDiagHTML(key){
  var gap=diagGAP[key]||30;
  var ctrl='<div style="display:flex;align-items:center;gap:6px;margin-bottom:8px;justify-content:flex-end">'
    +'<span style="font-size:11px;color:var(--muted);font-weight:600">Espaciado</span>'
    +'<button onclick="adjustDiagGAP(\''+key+'\',-10)" style="width:22px;height:22px;border:1.5px solid var(--borde);border-radius:5px;background:#fff;font-size:14px;font-weight:700;cursor:pointer;color:var(--muted);line-height:1;padding:0;display:flex;align-items:center;justify-content:center">−</button>'
    +'<span style="font-size:11px;font-weight:700;color:var(--azul-m);min-width:26px;text-align:center">'+gap+'</span>'
    +'<button onclick="adjustDiagGAP(\''+key+'\',10)" style="width:22px;height:22px;border:1.5px solid var(--borde);border-radius:5px;background:#fff;font-size:14px;font-weight:700;cursor:pointer;color:var(--muted);line-height:1;padding:0;display:flex;align-items:center;justify-content:center">+</button>'
    +'</div>';
  return '<div class="flow-diag-wrap">'+ctrl+renderFlowSVG(key)+'</div>';
}

function toggleFlowView(key, mode){
  var listEl  = document.getElementById('flujo-'+key+'-list');
  var diagEl  = document.getElementById('flujo-'+key+'-diag');
  var btnsEl  = document.getElementById('flujo-'+key+'-btns');
  var editBtn = document.getElementById('ftog-'+key+'-edit');
  var diagBtn = document.getElementById('ftog-'+key+'-diag');
  if(!listEl||!diagEl) return;
  if(mode==='diag'){
    diagEl.innerHTML = buildDiagHTML(key);
    listEl.style.display='none';
    if(btnsEl) btnsEl.style.display='none';
    diagEl.style.display='block';
    editBtn.classList.remove('active'); diagBtn.classList.add('active');
  } else {
    diagEl.style.display='none';
    listEl.style.display='block';
    if(btnsEl) btnsEl.style.display='flex';
    editBtn.classList.add('active'); diagBtn.classList.remove('active');
  }
}

function renderFlowSVG(key){
  var data = key==='actual' ? (flujosActual[activeTram]||[]) : (flujosPropuesto[activeTram]||[]);
  if(!data.length) return '<p style="color:var(--muted);font-size:13px;padding:2rem;text-align:center">Sin pasos definidos. Vuelva a Editar para agregar actividades.</p>';

  /* ── LAYOUT: flujo principal horizontal, decisiones EXTERNAS flotando arriba ── */
  var fills={inicio:'#dcfce7',fin:'#dbeafe',decision:'#fef9ec',paso:'#eef3ff'};
  var strks={inicio:'#16a34a',fin:'#2563eb',decision:'#d97706',paso:'#1455a4'};
  var NW={inicio:82,fin:82,decision:72,paso:100};
  var NH={inicio:30,fin:30,decision:72,paso:42};
  var GAP=diagGAP[key]||30, MLEFT=22, LT=9;
  function nw(t){return NW[t]||NW.paso;}
  function nh(t){return NH[t]||NH.paso;}
  function nhh(t){return nh(t)/2;}

  var mainIdx=[], decIdx=[];
  data.forEach(function(s,i){ (s.tipo||'paso')==='decision'?decIdx.push(i):mainIdx.push(i); });

  /* X del flujo principal */
  var cx=new Array(data.length).fill(0);
  var xp=MLEFT;
  mainIdx.forEach(function(i){ cx[i]=xp+nw(data[i].tipo||'paso')/2; xp+=nw(data[i].tipo||'paso')+GAP; });

  /* X de cada decisión:
     - Si la decisión ANTERIOR en el array también es decisión → encadenada a su IZQUIERDA
     - Si no → encima del predecesor (slot hacia la derecha para múltiples del mismo paso) */
  function findPred(di){ for(var k=di-1;k>=0;k--) if((data[k].tipo||'paso')!=='decision') return k; return -1; }
  var predSlot={};
  decIdx.forEach(function(di){
    var isChain=(di>0&&(data[di-1].tipo||'paso')==='decision');
    if(isChain){
      cx[di]=cx[di-1]-(nw('decision')+16);
    } else {
      var pred=findPred(di);
      var slot=predSlot[pred]||0; predSlot[pred]=slot+1;
      var baseX=pred>=0?cx[pred]:MLEFT+nw('decision')/2;
      cx[di]=baseX+slot*(nw('decision')+16);
    }
  });

  /* Si alguna decisión encadenada sale del margen izquierdo, desplazar todo a la derecha */
  var minDecLeft=decIdx.reduce(function(m,di){ return Math.min(m,cx[di]-nw('decision')/2); }, MLEFT);
  var shiftR=Math.max(0,MLEFT+10-minDecLeft);
  if(shiftR>0){ for(var si=0;si<data.length;si++) cx[si]+=shiftR; xp+=shiftR; }

  /* Ancho total */
  var maxDecX=decIdx.reduce(function(m,di){ return Math.max(m,cx[di]+nw('decision')/2); }, xp-GAP);
  var W=Math.max(xp-GAP,maxDecX)+70;

  /* Alturas — decisiones flotan bien SEPARADAS encima */
  var DH=nh('decision'), DW=nw('decision');
  var DEC_GAP=44;                  /* separación entre fondo del diamante y tope del paso */
  var TOP=DH+DEC_GAP+14;
  var BOT=54;
  var MY=TOP, H=MY+BOT;
  var DY=MY-DEC_GAP-DH/2;         /* centro Y de todas las decisiones */

  var els=[];
  els.push('<defs>'
    +'<marker id="aB" markerWidth="6" markerHeight="6" refX="6" refY="3" orient="auto"><path d="M0,0 L6,3 L0,6 Z" fill="#1455a4"/></marker>'
    +'<marker id="aA" markerWidth="5" markerHeight="5" refX="5" refY="2.5" orient="auto"><path d="M0,0 L5,2.5 L0,5 Z" fill="#d97706"/></marker>'
    +'<filter id="fs"><feDropShadow dx="0" dy="1" stdDeviation="1.2" flood-opacity="0.09"/></filter>'
    +'</defs>');

  /* Conectores horizontales del flujo principal (sin pasar por decisiones) */
  for(var k=1;k<mainIdx.length;k++){
    var pi=mainIdx[k-1], ci=mainIdx[k];
    var prevR=cx[pi]+nw(data[pi].tipo||'paso')/2;
    var curL =cx[ci]-nw(data[ci].tipo||'paso')/2;
    if(curL>prevR+2)
      els.push('<line x1="'+prevR+'" y1="'+MY+'" x2="'+(curL-1)+'" y2="'+MY+'" stroke="#1455a4" stroke-width="1.4" marker-end="url(#aB)"/>');
  }

  /* Flechas de DERIVACIÓN (vertical del paso) y CADENA (horizontal entre decisiones) */
  decIdx.forEach(function(di){
    var isChain=(di>0&&(data[di-1].tipo||'paso')==='decision');
    if(isChain){
      /* Cadena: punta izquierda del anterior → punta derecha de este */
      var x1=cx[di-1]-nw('decision')/2;
      var x2=cx[di]+nw('decision')/2;
      els.push('<line x1="'+x1+'" y1="'+DY+'" x2="'+(x2+1)+'" y2="'+DY
        +'" stroke="#1455a4" stroke-width="1.3" marker-end="url(#aB)"/>');
    } else {
      /* Derivación vertical del predecesor PASO/INICIO */
      var pred=findPred(di);
      if(pred<0) return;
      var bx=cx[di]-6;
      var srcTop=MY-nhh(data[pred].tipo||'paso');
      var decBot=DY+DH/2;
      els.push('<line x1="'+bx+'" y1="'+srcTop+'" x2="'+bx+'" y2="'+(decBot+1)
        +'" stroke="#1455a4" stroke-width="1.3" marker-end="url(#aB)"/>');
    }
  });

  /* Flechas de RETORNO: cada una en su propio carril (escalonadas) en el corredor entre decisiones y pasos */
  var retN=0;
  decIdx.forEach(function(di){
    var j=(data[di].retorno_a!==null&&data[di].retorno_a!==undefined&&data[di].retorno_a!=='')?parseInt(data[di].retorno_a):-1;
    if(j<0) return;
    var startX=cx[di]+6;
    var startY=DY+DH/2;               /* fondo del diamante */
    var targetType=data[j].tipo||'paso';
    var endX=cx[j]-nw(targetType)/2;  /* borde izquierdo del nodo destino */
    var endY=MY;                       /* nivel del flujo principal */
    var routeY=Math.min(DY+DH/2+8+retN*7, endY-14); /* carril escalonado por índice de retorno */
    retN++;
    var approachX=endX-14;
    var rH=Math.min(6,Math.abs(startX-approachX)/2);
    var rV=Math.min(rH,(endY-routeY)/2-1);
    var r=Math.max(2,rV);
    var goLeft=startX>approachX+r;
    var hTurn=goLeft?startX-r:startX+r;
    var hArrive=goLeft?approachX+r:approachX-r;
    var d='M'+startX+' '+startY
         +' L'+startX+' '+(routeY-r)
         +' Q'+startX+' '+routeY+' '+hTurn+' '+routeY
         +' L'+hArrive+' '+routeY
         +' Q'+approachX+' '+routeY+' '+approachX+' '+(routeY+r)
         +' L'+approachX+' '+(endY-r)
         +' Q'+approachX+' '+endY+' '+(approachX+r)+' '+endY
         +' L'+endX+' '+endY;
    els.push('<path d="'+d+'" fill="none" stroke="#d97706" stroke-width="1.2" stroke-dasharray="4,3" marker-end="url(#aA)"/>');
  });

  /* Nodos */
  data.forEach(function(step,i){
    var tipo=step.tipo||'paso';
    var ncx=cx[i], ncy=(tipo==='decision'?DY:MY);
    var f=fills[tipo]||fills.paso, s=strks[tipo]||strks.paso;
    var nwi=nw(tipo), nhhi=nhh(tipo);
    var oc=' onclick="showNodeModal(\''+key+'\','+i+')" style="cursor:pointer"';

    if(tipo==='inicio'||tipo==='fin'){
      els.push('<ellipse cx="'+ncx+'" cy="'+ncy+'" rx="'+(nwi/2)+'" ry="'+nhhi+'" fill="'+f+'" stroke="'+s+'" stroke-width="1.4" filter="url(#fs)"'+oc+'><title>Ver detalles</title></ellipse>');
    } else if(tipo==='decision'){
      els.push('<polygon points="'+ncx+','+(ncy-nhhi)+' '+(ncx+nwi/2)+','+ncy+' '+ncx+','+(ncy+nhhi)+' '+(ncx-nwi/2)+','+ncy
        +'" fill="'+f+'" stroke="'+s+'" stroke-width="1.4" filter="url(#fs)"'+oc+'><title>Ver detalles</title></polygon>');
    } else {
      els.push('<rect x="'+(ncx-nwi/2)+'" y="'+(ncy-nhhi)+'" width="'+nwi+'" height="'+(nhhi*2)+'" rx="5" fill="'+f+'" stroke="'+s+'" stroke-width="1.4" filter="url(#fs)"'+oc+'><title>Ver detalles</title></rect>');
    }

    var bx=ncx-nwi/2+(tipo==='decision'?5:8), by=ncy-nhhi+(tipo==='decision'?11:9);
    els.push('<circle cx="'+bx+'" cy="'+by+'" r="7.5" fill="'+(strks[tipo]||strks.paso)+'"/>');
    els.push('<text x="'+bx+'" y="'+by+'" text-anchor="middle" dominant-baseline="middle" font-size="6.5" font-weight="800" fill="#fff" font-family="Segoe UI,sans-serif" style="pointer-events:none">'+(i+1)+'</text>');

    var maxC=tipo==='decision'?7:13;
    var tl=wrapText(step.titulo||(TIPO_LABELS[tipo]||'Paso'),maxC).slice(0,tipo==='decision'?3:2);
    var ty=ncy-((tl.length-1)*LT/2);
    tl.forEach(function(ln,li){
      els.push('<text x="'+ncx+'" y="'+(ty+li*LT)+'" text-anchor="middle" dominant-baseline="middle" font-size="9" font-weight="700" fill="#0a2d6e" font-family="Segoe UI,sans-serif" style="pointer-events:none">'+escHtml(ln)+'</text>');
    });

    /* Botón "+" para agregar nodos — no aparece en FIN */
    if(tipo !== 'fin'){
      var addBX=ncx+nwi/2+12, addBY=(tipo==='decision'?DY:MY);
      var addC=strks[tipo]||strks.paso;
      var addOC='showFlowMenu(event,\''+key+'\','+i+',\''+tipo+'\')';
      els.push('<g onclick="'+addOC+';event.stopPropagation()" style="cursor:pointer">'
        +'<circle cx="'+addBX+'" cy="'+addBY+'" r="7.5" fill="'+addC+'" opacity="0.82"/>'
        +'<text x="'+addBX+'" y="'+addBY+'" text-anchor="middle" dominant-baseline="middle" font-size="13" font-weight="700" fill="#fff" font-family="Segoe UI,sans-serif" style="pointer-events:none">+</text>'
        +'</g>');
    }
  });

  els.push('<text x="'+(W/2)+'" y="'+(H-5)+'" text-anchor="middle" font-size="7" fill="#b0bec5" font-style="italic" font-family="Segoe UI,sans-serif">Clic en nodo para ver · (+) para agregar</text>');

  return '<svg viewBox="0 0 '+W+' '+H+'" xmlns="http://www.w3.org/2000/svg" style="width:'+W+'px;height:'+H+'px;max-width:none;display:block">'
    +els.join('')+'</svg>';
}

function showNodeModal(key, idx){
  var data = key==='actual' ? (flujosActual[activeTram]||[]) : (flujosPropuesto[activeTram]||[]);
  var step = data[idx];
  if(!step) return;
  var tipo = step.tipo||'paso';
  var colors={inicio:'#16a34a',fin:'#2563eb',decision:'#d97706',paso:'#1455a4'};
  var labels={inicio:'INICIO',fin:'FIN',decision:'DECISIÓN',paso:'PASO'};
  document.getElementById('nm-badge').textContent = (idx+1);
  document.getElementById('nm-badge').style.background = colors[tipo]||colors.paso;
  document.getElementById('nm-tipo').textContent = labels[tipo]||'PASO';
  document.getElementById('nm-tipo').style.color = colors[tipo]||colors.paso;
  document.getElementById('nm-titulo').textContent = step.titulo||(labels[tipo]||'Paso');
  var rows=[
    {id:'nm-responsable', label:'Responsable', val:step.area},
    {id:'nm-tiempo',      label:'Tiempo observado', val:step.tiempo},
    {id:'nm-documento',   label:'Documento emitido', val:step.doc_emitido},
    {id:'nm-obs',         label:'Observación de campo', val:step.obs}
  ];
  rows.forEach(function(r){
    var el=document.getElementById(r.id);
    if(el){ el.textContent=r.val||'—'; el.style.color=r.val?'#1e293b':'#94a3b8'; }
  });
  document.getElementById('node-modal').style.display='flex';
}

function closeNodeModal(){
  document.getElementById('node-modal').style.display='none';
}

document.addEventListener('keydown',function(e){ if(e.key==='Escape'){ closeNodeModal(); closeFlowMenu(); } });

/* ── MENÚ FLOTANTE "+" ── */
function showFlowMenu(event, key, idx, tipo){
  event.stopPropagation();
  var menu=document.getElementById('flow-add-menu');
  var html='';
  if(tipo==='inicio'){
    html='<div class="fam-opt" onclick="execAddNode(\''+key+'\','+idx+',\'paso\');closeFlowMenu()">▬ Paso</div>';
  } else if(tipo==='paso'){
    html='<div class="fam-opt" onclick="execAddNode(\''+key+'\','+idx+',\'decision\');closeFlowMenu()"><span style="color:#d97706">◆</span> Decisión</div>'
        +'<div class="fam-opt" onclick="execAddNode(\''+key+'\','+idx+',\'paso\');closeFlowMenu()">▬ Paso</div>';
  } else if(tipo==='decision'){
    html='<div class="fam-opt" onclick="execAddNode(\''+key+'\','+idx+',\'decision\');closeFlowMenu()"><span style="color:#d97706">◆</span> Decisión</div>'
        +'<div class="fam-opt fam-ret" onclick="showRetornoMenu(event,\''+key+'\','+idx+')">↩ Retorno</div>';
  }
  menu.innerHTML=html;
  menu.style.display='block';
  menu.style.left=(event.clientX+8)+'px';
  menu.style.top=(event.clientY-16)+'px';
}

function closeFlowMenu(){
  document.getElementById('flow-add-menu').style.display='none';
}
document.addEventListener('click', closeFlowMenu);

function execAddNode(key, afterIdx, tipo){
  var arr=key==='actual'?flujosActual[activeTram]:flujosPropuesto[activeTram];
  var insertAt=afterIdx+1;
  if(tipo!=='decision'){
    while(insertAt<arr.length&&(arr[insertAt].tipo||'paso')==='decision') insertAt++;
  }
  var node={tipo:tipo,titulo:'',area:'',tiempo:'',doc_emitido:'',obs:'',retorno_a:null};
  arr.splice(insertAt,0,node);
  arr.forEach(function(s){
    if(s.retorno_a!==null&&s.retorno_a!==undefined&&parseInt(s.retorno_a)>=insertAt)
      s.retorno_a=parseInt(s.retorno_a)+1;
  });
  if(key==='actual') renderFlujosActual(); else renderFlujosPropuesto();
  refreshDiagIfVisible(key);
  actualizarBVA();
}

function refreshDiagIfVisible(key){
  var el=document.getElementById('flujo-'+key+'-diag');
  if(el&&el.style.display!=='none')
    el.innerHTML=buildDiagHTML(key);
}

function showRetornoMenu(event, key, decIdx){
  event.stopPropagation();
  var arr=key==='actual'?flujosActual[activeTram]:flujosPropuesto[activeTram];
  var labels={inicio:'INICIO',fin:'FIN',paso:'Paso',decision:'Decisión'};
  var html='<div style="font-size:10px;font-weight:700;color:#94a3b8;padding:4px 10px 6px;border-bottom:1px solid #f0f4fa;margin-bottom:3px">↩ Retorna a:</div>';
  var hasOpts=false;
  for(var j=0;j<decIdx;j++){
    var t=arr[j].tipo||'paso';
    if(t==='decision') continue;
    hasOpts=true;
    var isSel=parseInt(arr[decIdx].retorno_a)===j;
    var lbl=escHtml((arr[j].titulo||labels[t]||'Paso '+(j+1)).slice(0,24));
    html+='<div class="fam-opt'+(isSel?' fam-sel':'')+'" onclick="setRetorno(\''+key+'\','+decIdx+','+j+');closeFlowMenu()">'+lbl+'</div>';
  }
  if(!hasOpts) html+='<div style="font-size:11px;color:#94a3b8;padding:5px 10px">Sin nodos anteriores</div>';
  if(arr[decIdx].retorno_a!==null&&arr[decIdx].retorno_a!==undefined&&arr[decIdx].retorno_a!=='')
    html+='<div class="fam-opt fam-del" onclick="setRetorno(\''+key+'\','+decIdx+',null);closeFlowMenu()">✕ Quitar retorno</div>';
  var menu=document.getElementById('flow-add-menu');
  menu.innerHTML=html;
  menu.style.left=(event.clientX+8)+'px';
  menu.style.top=(event.clientY-16)+'px';
}

function setRetorno(key, decIdx, val){
  var arr=key==='actual'?flujosActual[activeTram]:flujosPropuesto[activeTram];
  arr[decIdx].retorno_a=(val===null)?null:parseInt(val);
  if(key==='actual') renderFlujosActual(); else renderFlujosPropuesto();
  refreshDiagIfVisible(key);
  actualizarBVA();
}

function wrapText(text,maxChars){
  var ws=(text||'').split(' '),ls=[],c='';
  ws.forEach(function(w){ var t=c?c+' '+w:w; if(t.length<=maxChars){c=t;}else{if(c)ls.push(c);c=w;}});
  if(c)ls.push(c); return ls.length?ls:[text||''];
}
