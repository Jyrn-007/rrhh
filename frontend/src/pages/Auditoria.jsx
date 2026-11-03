import { useCallback, useEffect, useState } from 'react';
import { api, errMsg } from '../api/client.js';
import { Alert, Badge, Pager, fmtFechaHora } from '../components/ui.jsx';

const TABS = [
  { id: 'operaciones', label: 'Operaciones', url: '/api/auditoria' },
  { id: 'accesos', label: 'Accesos', url: '/api/auditoria/accesos' },
  { id: 'logs', label: 'Logs del sistema', url: '/api/auditoria/logs' },
];

const tonoEvento = (e, ok) => (ok ? 'ok' : e === 'BLOQUEO' ? 'warn' : 'bad');
const tonoNivel = (n) => (n === 'ERROR' ? 'bad' : n === 'WARN' ? 'warn' : 'mute');

function Json({ label, value }) {
  if (!value) return null;
  let txt = value;
  try { txt = JSON.stringify(JSON.parse(value), null, 2); } catch { /* texto plano */ }
  return (
    <div>
      <div className="text-[10px] uppercase text-mute mb-1">{label}</div>
      <pre className="text-[11px] font-mono bg-canvas border border-line rounded p-2 overflow-x-auto max-h-48">{txt}</pre>
    </div>
  );
}

export default function Auditoria() {
  const [tab, setTab] = useState('operaciones');
  const [filtros, setFiltros] = useState({ desde: '', hasta: '', texto: '' });
  const [page, setPage] = useState(1);
  const [data, setData] = useState({ items: [], total: 0 });
  const [error, setError] = useState('');
  const pageSize = 30;

  const cargar = useCallback(() => {
    const t = TABS.find((x) => x.id === tab);
    const params = { page, pageSize, desde: filtros.desde || undefined, hasta: filtros.hasta || undefined };
    if (filtros.texto) {
      if (tab === 'logs') params.nivel = filtros.texto.toUpperCase();
      else params.usuario = filtros.texto;
    }
    api.get(t.url, { params }).then((r) => { setData(r.data); setError(''); }).catch((e) => setError(errMsg(e)));
  }, [tab, page, filtros]);

  useEffect(() => { const t = setTimeout(cargar, 250); return () => clearTimeout(t); }, [cargar]);

  const cambiarTab = (id) => { setTab(id); setPage(1); setFiltros((f) => ({ ...f, texto: '' })); };

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold">Auditoría y logs</h1>

      <div className="flex gap-1 border-b border-line">
        {TABS.map((t) => (
          <button key={t.id} onClick={() => cambiarTab(t.id)}
            className={`px-4 py-2 text-sm -mb-px border-b-2 ${tab === t.id ? 'border-fox text-fox' : 'border-transparent text-mute hover:text-ink'}`}>
            {t.label}
          </button>
        ))}
      </div>

      <div className="flex flex-wrap gap-3 items-end">
        <input className="input max-w-[14rem]" placeholder={tab === 'logs' ? 'Nivel (ERROR, WARN…)' : 'Usuario…'} value={filtros.texto}
          onChange={(e) => { setPage(1); setFiltros({ ...filtros, texto: e.target.value }); }} />
        <div><span className="label">Desde</span><input type="date" className="input" value={filtros.desde} onChange={(e) => { setPage(1); setFiltros({ ...filtros, desde: e.target.value }); }} /></div>
        <div><span className="label">Hasta</span><input type="date" className="input" value={filtros.hasta} onChange={(e) => { setPage(1); setFiltros({ ...filtros, hasta: e.target.value }); }} /></div>
      </div>

      <Alert>{error}</Alert>

      <div className="card overflow-x-auto">
        {tab === 'operaciones' && (
          <table className="w-full">
            <thead><tr><th className="th">Fecha</th><th className="th">Usuario</th><th className="th">Acción</th><th className="th">Entidad</th><th className="th">IP</th></tr></thead>
            <tbody>
              {data.items.map((a) => (
                <tr key={a.id} className="align-top">
                  <td className="td whitespace-nowrap text-xs">{fmtFechaHora(a.fecha)}</td>
                  <td className="td">{a.username ?? '—'}</td>
                  <td className="td"><Badge tone="fox">{a.accion}</Badge></td>
                  <td className="td">
                    <details>
                      <summary className="cursor-pointer">{a.entidad} <span className="text-mute font-mono">#{a.entidadId}</span></summary>
                      <div className="grid md:grid-cols-2 gap-2 mt-2"><Json label="Antes" value={a.valoresAntes} /><Json label="Después" value={a.valoresDespues} /></div>
                    </details>
                  </td>
                  <td className="td font-mono text-xs text-mute">{a.ip}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
        {tab === 'accesos' && (
          <table className="w-full">
            <thead><tr><th className="th">Fecha</th><th className="th">Usuario</th><th className="th">Evento</th><th className="th">Detalle</th><th className="th">IP</th></tr></thead>
            <tbody>
              {data.items.map((a) => (
                <tr key={a.id}>
                  <td className="td whitespace-nowrap text-xs">{fmtFechaHora(a.fecha)}</td>
                  <td className="td">{a.username}</td>
                  <td className="td"><Badge tone={tonoEvento(a.evento, a.exitoso)}>{a.evento}</Badge></td>
                  <td className="td text-xs text-mute">{a.detalle}</td>
                  <td className="td font-mono text-xs text-mute">{a.ip}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
        {tab === 'logs' && (
          <table className="w-full">
            <thead><tr><th className="th">Fecha</th><th className="th">Nivel</th><th className="th">Origen</th><th className="th">Mensaje</th><th className="th">Ruta</th></tr></thead>
            <tbody>
              {data.items.map((l) => (
                <tr key={l.id}>
                  <td className="td whitespace-nowrap text-xs">{fmtFechaHora(l.fecha)}</td>
                  <td className="td"><Badge tone={tonoNivel(l.nivel)}>{l.nivel}</Badge></td>
                  <td className="td text-xs">{l.origen}</td>
                  <td className="td text-xs">{l.mensaje}</td>
                  <td className="td font-mono text-xs text-mute">{l.metodo} {l.ruta}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
        {data.items.length === 0 && <div className="td text-mute">Sin registros.</div>}
        <Pager page={page} pageSize={pageSize} total={data.total} onPage={setPage} />
      </div>
    </div>
  );
}
