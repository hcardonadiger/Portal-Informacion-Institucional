(() => {
  'use strict';
  const $ = id => document.getElementById(id);
  const MONTHS = ['Enero','Febrero','Marzo','Abril','Mayo','Junio','Julio','Agosto','Septiembre','Octubre','Noviembre','Diciembre'];
  const STATUS = { finalizada:'Finalizadas', en_proceso:'En proceso', sin_iniciar:'Sin iniciar', vencida:'Vencidas', pendiente:'Pendientes' };
  const COMPLIANCE = { dentro_plazo:'Dentro del plazo', fuera_plazo:'Fuera del plazo', sin_informacion:'Sin información suficiente' };
  const COLORS = ['#1455a4','#17825c','#d97706','#dc2626','#7c3aed','#0891b2','#64748b'];
  let source, charts = {}, page = 1, sort = { key:'total', dir:-1 }, detailChart;

  const avg = xs => { xs = xs.filter(Number.isFinite); return xs.length ? xs.reduce((a,b) => a+b,0) / xs.length : null; };
  const median = xs => { xs = xs.filter(Number.isFinite).sort((a,b)=>a-b); if (!xs.length) return null; const m=Math.floor(xs.length/2); return xs.length%2 ? xs[m] : (xs[m-1]+xs[m])/2; };
  const number = value => Number.isFinite(value) ? new Intl.NumberFormat('es-HN',{maximumFractionDigits:1}).format(value) : 'N/D';
  const integer = value => new Intl.NumberFormat('es-HN').format(value || 0);
  const percent = value => Number.isFinite(value) ? `${new Intl.NumberFormat('es-HN',{maximumFractionDigits:1}).format(value)}%` : 'N/D';
  const escape = value => String(value ?? '').replace(/[&<>"]/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;'}[c]));
  const meta = code => source.instituciones.find(x => x.codigo === code) || { codigo:code, sigla:code, nombre:null };
  const label = code => { const i=meta(code); return i.nombre || i.sigla || i.codigo; };
  const setOptions = (id, values, text) => { const el=$(id); el.insertAdjacentHTML('beforeend', values.map(v=>`<option value="${escape(v.value)}">${escape(text(v))}</option>`).join('')); };

  function filtered() {
    const text = $('ti-search').value.trim().toLocaleLowerCase();
    const filters = { institution:$('ti-institution').value, procedure:$('ti-procedure').value, year:$('ti-year').value, month:$('ti-month').value, status:$('ti-status').value, compliance:$('ti-compliance').value };
    return source.registros.filter(r => (!filters.institution || r.institucion===filters.institution)
      && (!filters.procedure || `${r.institucion}|${r.tramiteCodigo}`===filters.procedure)
      && (!filters.year || String(r.anio)===filters.year) && (!filters.month || String(r.mes)===filters.month)
      && (!filters.status || r.estado===filters.status) && (!filters.compliance || r.cumplimiento===filters.compliance)
      && (!text || `${label(r.institucion)} ${r.institucion} ${r.tramite || ''}`.toLocaleLowerCase().includes(text)));
  }

  function stats(records) {
    const finalizadas = records.filter(r=>r.estado==='finalizada');
    const withCompliance = finalizadas.filter(r=>r.cumplimiento!=='sin_informacion');
    return { total:records.length, procedures:new Set(records.map(r=>`${r.institucion}|${r.tramiteCodigo}`)).size,
      finished:finalizadas.length, completion: records.length ? finalizadas.length*100/records.length : null,
      expectedAvg:avg(records.map(r=>r.diasEstimados)), expectedMedian:median(records.map(r=>r.diasEstimados)),
      realAvg:avg(finalizadas.map(r=>r.diasReales)), realMedian:median(finalizadas.map(r=>r.diasReales)), offsetAvg:avg(finalizadas.map(r=>r.desfase)),
      onTime:withCompliance.length ? withCompliance.filter(r=>r.cumplimiento==='dentro_plazo').length*100/withCompliance.length : null };
  }

  function byInstitution(records) {
    const groups = new Map();
    records.forEach(r=>{ if(!groups.has(r.institucion)) groups.set(r.institucion,[]); groups.get(r.institucion).push(r); });
    return [...groups].map(([code, rows])=>({ code, name:label(code), sigla:meta(code).sigla || code, ...stats(rows), rows }));
  }

  function updateChart(id, type, labels, datasets, options={}) {
    charts[id]?.destroy();
    charts[id] = new Chart($(id), { type, data:{ labels, datasets }, options:{ responsive:true, maintainAspectRatio:false, plugins:{ legend:{ position:'bottom' }, tooltip:{ callbacks:{ label:c=>`${c.dataset.label ? c.dataset.label+': ' : ''}${number(c.raw)}` } } }, ...options } });
  }

  function renderKpis(rows) {
    const s=stats(rows), institutions=new Set(rows.map(r=>r.institucion)).size;
    const items=[['🏛','Instituciones incorporadas',integer(institutions),'Instituciones con solicitudes en la vista actual'],['◈','Trámites únicos',integer(s.procedures),'Combinaciones institución y código de trámite'],['▤','Solicitudes recibidas',integer(s.total),'Registros de solicitudes'],['✓','Solicitudes finalizadas',integer(s.finished),'Solicitudes con fecha de finalización'],['%','Porcentaje de finalización',percent(s.completion),'Finalizadas entre solicitudes recibidas'],['◷','Promedio días estimados',number(s.expectedAvg),'Valores vacíos no se consideran'],['◴','Promedio días reales',number(s.realAvg),'Solo solicitudes finalizadas'],['◉','Atendidas dentro del plazo',percent(s.onTime),'Finalizadas con desfase menor o igual a cero']];
    $('ti-kpis').innerHTML=items.map(([icon,title,value,help])=>`<article class="ti-kpi" title="${escape(help)}"><span class="ti-kpi-icon">${icon}</span><div><strong>${value}</strong><span>${title}</span></div></article>`).join('');
  }

  function renderCharts(rows, institutions) {
    const top=[...institutions].sort((a,b)=>b.total-a.total).slice(0,12);
    updateChart('ti-requests-chart','bar',top.map(x=>x.sigla),[{label:'Solicitudes',data:top.map(x=>x.total),backgroundColor:'#1455a4'}],{indexAxis:'y',plugins:{legend:{display:false}}});
    updateChart('ti-procedures-chart','bar',top.map(x=>x.sigla),[{label:'Trámites únicos',data:top.map(x=>x.procedures),backgroundColor:'#17825c'}],{plugins:{legend:{display:false}}});
    const valid=institutions.filter(x=>x.expectedAvg!==null || x.realAvg!==null).sort((a,b)=>b.total-a.total).slice(0,12);
    updateChart('ti-days-chart','bar',valid.map(x=>x.sigla),[{label:'Días estimados',data:valid.map(x=>x.expectedAvg),backgroundColor:'#1455a4'},{label:'Días reales',data:valid.map(x=>x.realAvg),backgroundColor:'#d97706'}]);
    const yearly=$('ti-trend-view').value==='yearly', trend=new Map();
    rows.forEach(r=>{ const k=yearly?String(r.anio||'Sin fecha'):`${r.anio||'Sin fecha'}-${String(r.mes||0).padStart(2,'0')}`; trend.set(k,(trend.get(k)||0)+1); });
    const trendItems=[...trend].sort((a,b)=>a[0].localeCompare(b[0]));
    updateChart('ti-trend-chart','line',trendItems.map(([k])=>yearly?k:(k.endsWith('-00')?'Sin mes':`${MONTHS[Number(k.slice(-2))-1]} ${k.slice(0,4)}`)),[{label:'Solicitudes',data:trendItems.map(([,v])=>v),borderColor:'#1455a4',backgroundColor:'rgba(20,85,164,.12)',fill:true,tension:.25}],{scales:{y:{beginAtZero:true,ticks:{precision:0}}}});
    const count=(field, keys)=>keys.map(k=>rows.filter(r=>r[field]===k).length);
    updateChart('ti-status-chart','doughnut',Object.keys(STATUS).map(k=>STATUS[k]),[{label:'Solicitudes',data:count('estado',Object.keys(STATUS)),backgroundColor:['#17825c','#1455a4','#64748b','#dc2626','#7c3aed']}]);
    updateChart('ti-compliance-chart','doughnut',Object.keys(COMPLIANCE).map(k=>COMPLIANCE[k]),[{label:'Solicitudes',data:count('cumplimiento',Object.keys(COMPLIANCE)),backgroundColor:['#17825c','#dc2626','#94a3b8']}]);
  }

  function renderTable(institutions) {
    const sorted=[...institutions].sort((a,b)=>{ const av=a[sort.key], bv=b[sort.key]; return typeof av==='string' ? sort.dir*av.localeCompare(bv) : sort.dir*((av??-Infinity)-(bv??-Infinity)); });
    const size=10, pages=Math.max(1,Math.ceil(sorted.length/size)); page=Math.min(page,pages);
    const shown=sorted.slice((page-1)*size,page*size), tbody=$('#ti-table tbody');
    tbody.innerHTML=shown.length?shown.map(x=>`<tr><td>${escape(x.name)}${meta(x.code).pendienteConfirmacion?'<small title="Nombre pendiente de confirmar"> Código SOL</small>':''}</td><td>${escape(x.sigla)}</td><td>${integer(x.procedures)}</td><td>${integer(x.total)}</td><td>${integer(x.finished)}</td><td>${percent(x.completion)}</td><td>${number(x.expectedAvg)}</td><td>${number(x.expectedMedian)}</td><td>${number(x.realAvg)}</td><td>${number(x.realMedian)}</td><td>${number(x.offsetAvg)}</td><td>${percent(x.onTime)}</td><td><button class="btns ti-detail-btn" data-code="${escape(x.code)}">Ver detalle</button></td></tr>`).join(''):'<tr><td colspan="13" class="ti-empty">No hay resultados para los filtros seleccionados.</td></tr>';
    $('ti-table-summary').textContent=`${integer(sorted.length)} instituciones · página ${page} de ${pages}`; $('ti-page-number').textContent=`${page}/${pages}`; $('ti-prev').disabled=page===1; $('ti-next').disabled=page===pages;
    $$('#ti-table .ti-detail-btn').forEach(b=>b.addEventListener('click',()=>openDetail(b.dataset.code)));
  }

  function openDetail(code) {
    const rows=filtered().filter(r=>r.institucion===code), s=stats(rows), institution=meta(code), perProcedure=new Map();
    rows.forEach(r=>{const k=`${r.tramiteCodigo}|${r.tramite||'Sin nombre'}`; if(!perProcedure.has(k)) perProcedure.set(k,[]); perProcedure.get(k).push(r);});
    $('ti-detail-title').textContent=institution.nombre || institution.sigla || code;
    $('ti-detail-kpis').innerHTML=[['Solicitudes',integer(s.total)],['Trámites',integer(s.procedures)],['Finalizadas',integer(s.finished)],['% finalización',percent(s.completion)],['Prom. estimado',number(s.expectedAvg)],['Prom. real',number(s.realAvg)],['En plazo',percent(s.onTime)]].map(x=>`<div><strong>${x[1]}</strong><span>${x[0]}</span></div>`).join('');
    const procedureRows=[...perProcedure].map(([key,records])=>({key, s:stats(records)})).sort((a,b)=>b.s.total-a.s.total);
    $('#ti-detail-table tbody').innerHTML=procedureRows.map(({key,s})=>{const [code,name]=key.split('|'); return `<tr><td>${escape(code)}</td><td>${escape(name)}</td><td>${integer(s.total)}</td><td>${number(s.expectedAvg)}</td><td>${number(s.realAvg)}</td><td>${number(s.offsetAvg)}</td><td>${integer(s.finished)}</td><td>${integer(s.total-s.finished)}</td><td>${percent(s.onTime)}</td></tr>`;}).join('') || '<tr><td colspan="9" class="ti-empty">Sin datos.</td></tr>';
    const monthly=new Map(); rows.forEach(r=>{const k=`${r.anio||'Sin fecha'}-${String(r.mes||0).padStart(2,'0')}`;monthly.set(k,(monthly.get(k)||0)+1);}); const series=[...monthly].sort((a,b)=>a[0].localeCompare(b[0]));
    detailChart?.destroy(); detailChart=new Chart($('ti-detail-chart'),{type:'line',data:{labels:series.map(([k])=>k),datasets:[{label:'Solicitudes',data:series.map(([,v])=>v),borderColor:'#1455a4',tension:.25}]},options:{responsive:true,maintainAspectRatio:false,plugins:{legend:{display:false}}}});
    $('ti-detail').showModal();
  }

  const $$ = selector => [...document.querySelectorAll(selector)];
  function render() { const rows=filtered(), institutions=byInstitution(rows); renderKpis(rows); renderCharts(rows,institutions); renderTable(institutions); const active=['ti-search','ti-institution','ti-procedure','ti-year','ti-month','ti-status','ti-compliance'].filter(id=>$(id).value).length; $('ti-active-filters').hidden=!active; $('ti-active-filters').textContent=`${active} activo${active===1?'':'s'}`; }
  function exportCsv() { const rows=byInstitution(filtered()); const head=['Institución','Sigla','Trámites','Solicitudes','Finalizadas','% finalización','Promedio estimado','Mediana estimada','Promedio real','Mediana real','Desfase promedio','% en plazo']; const lines=[head,...rows.map(x=>[x.name,x.sigla,x.procedures,x.total,x.finished,x.completion,x.expectedAvg,x.expectedMedian,x.realAvg,x.realMedian,x.offsetAvg,x.onTime])].map(row=>row.map(v=>`"${String(v??'').replaceAll('"','""')}"`).join(',')); const blob=new Blob(['\ufeff'+lines.join('\n')],{type:'text/csv;charset=utf-8'}), a=document.createElement('a');a.href=URL.createObjectURL(blob);a.download='indicadores-tramites-filtrados.csv';a.click();URL.revokeObjectURL(a.href); }

  async function init() {
    try { const res=await fetch('?handler=Datos'); if(!res.ok) throw new Error('No fue posible obtener los datos.'); source=await res.json(); if(source.metadata.registros!==24684) console.warn('Cantidad de registros distinta a la esperada:',source.metadata.registros); $('ti-updated').textContent=`Actualizado: ${source.metadata.actualizado} · ${integer(source.metadata.registros)} solicitudes`; setOptions('ti-institution',source.instituciones,i=>`${i.nombre||i.sigla||i.codigo} (${i.codigo})`); const procs=[...new Map(source.registros.map(r=>[`${r.institucion}|${r.tramiteCodigo}`,r])).values()].sort((a,b)=>(a.tramite||'').localeCompare(b.tramite||'')); setOptions('ti-procedure',procs,r=>`${r.tramite || 'Sin nombre'} · ${r.tramiteCodigo} (${meta(r.institucion).sigla||r.institucion})`); setOptions('ti-year',[...new Set(source.registros.map(r=>r.anio).filter(Boolean))].sort((a,b)=>b-a).map(value=>({value})),x=>x.value); setOptions('ti-month',MONTHS.map((name,index)=>({value:index+1,name})),x=>x.name); ['ti-search','ti-institution','ti-procedure','ti-year','ti-month','ti-status','ti-compliance'].forEach(id=>$(id).addEventListener(id==='ti-search'?'input':'change',()=>{page=1;render();})); $('ti-trend-view').addEventListener('change',render); $('ti-clear').addEventListener('click',()=>{['ti-search','ti-institution','ti-procedure','ti-year','ti-month','ti-status','ti-compliance'].forEach(id=>$(id).value='');page=1;render();}); $('ti-prev').addEventListener('click',()=>{page--;render();}); $('ti-next').addEventListener('click',()=>{page++;render();}); $('ti-export').addEventListener('click',exportCsv); $$('#ti-table th[data-sort]').forEach(th=>th.addEventListener('click',()=>{const key=th.dataset.sort;sort={key,dir:sort.key===key?-sort.dir:-1};renderTable(byInstitution(filtered()));})); $('ti-detail-close').addEventListener('click',()=>$('ti-detail').close()); $('ti-detail').addEventListener('click',e=>{if(e.target===$('ti-detail')) $('ti-detail').close();}); $('ti-loading').hidden=true;$('ti-content').hidden=false;render(); } catch(error) { $('ti-loading').hidden=true;$('ti-error').textContent=error.message;$('ti-error').hidden=false; } }
  init();
})();
