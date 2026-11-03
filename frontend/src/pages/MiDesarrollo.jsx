import { useState } from 'react';
import { api } from '../api/client.js';
import { ejecutar, useApi } from '../api/useApi.js';
import { Alert, EstadoBadge, Estrellas, SinEmpleado, Tabs, fmtFecha } from '../components/ui.jsx';

function Objetivo({ o, onSaved, setErr }) {
  const [p, setP] = useState(o.progreso);
  const [e, setE] = useState(o.estado === 'NO_CUMPLIDO' ? 'NO_CUMPLIDO' : o.estado);
  const cerrado = o.estado === 'CUMPLIDO' || o.estado === 'NO_CUMPLIDO';

  async function guardar() {
    const body = { titulo: o.titulo, descripcion: o.descripcion, fechaLimite: o.fechaLimite, progreso: Number(p), estado: e };
    if (await ejecutar(() => api.put(`/api/desarrollo/objetivos/${o.id}`, body), setErr)) onSaved();
  }

  return (
    <div className="card p-4">
      <div className="flex justify-between gap-2">
        <div><div className="font-medium">{o.titulo}</div>{o.descripcion && <div className="text-xs text-mute">{o.descripcion}</div>}</div>
        <EstadoBadge estado={o.estado} />
      </div>
      <div className="text-xs text-mute mt-1">Fecha límite: {fmtFecha(o.fechaLimite)}</div>
      <div className="h-2 rounded bg-canvas overflow-hidden mt-3"><div className="h-full bg-fox" style={{ width: `${o.progreso}%` }} /></div>
      {!cerrado && (
        <div className="flex flex-wrap items-center gap-3 mt-3">
          <input type="range" min="0" max="100" step="5" value={p} onChange={(x) => setP(x.target.value)} className="accent-orange-500" />
          <span className="text-xs font-mono w-10">{p}%</span>
          <select className="input max-w-[11rem]" value={e} onChange={(x) => setE(x.target.value)}>
            <option value="PENDIENTE">Pendiente</option><option value="EN_PROGRESO">En progreso</option><option value="CUMPLIDO">Cumplido</option>
          </select>
          <button className="btn btn-sm" onClick={guardar}>Guardar avance</button>
        </div>
      )}
    </div>
  );
}

export default function MiDesarrollo() {
  const [tab, setTab] = useState('objetivos');
  const caps = useApi('/api/capacitaciones/mias');
  const evals = useApi('/api/desarrollo/evaluaciones/mias');
  const objs = useApi('/api/desarrollo/objetivos/mios');
  const [err, setErr] = useState('');

  if (caps.error?.includes('vinculado')) return <SinEmpleado />;

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold">Mi desarrollo</h1>
      <Tabs value={tab} onChange={setTab} tabs={[{ id: 'objetivos', label: 'Objetivos' }, { id: 'evaluaciones', label: 'Evaluaciones' }, { id: 'formacion', label: 'Formación' }]} />
      <Alert>{err}</Alert>

      {tab === 'objetivos' && (
        <div className="grid md:grid-cols-2 gap-4">
          {(objs.data ?? []).map((o) => <Objetivo key={o.id} o={o} setErr={setErr} onSaved={objs.reload} />)}
          {objs.data?.length === 0 && <p className="text-sm text-mute">No tiene objetivos asignados.</p>}
        </div>
      )}

      {tab === 'evaluaciones' && (
        <div className="card overflow-x-auto">
          <table className="w-full">
            <thead><tr><th className="th">Periodo</th><th className="th">Fecha</th><th className="th">Productividad</th><th className="th">Calidad</th><th className="th">Equipo</th><th className="th">Puntualidad</th><th className="th">Total</th></tr></thead>
            <tbody>
              {(evals.data ?? []).map((e) => (
                <tr key={e.id} className="align-top">
                  <td className="td">{e.periodo}{e.comentarios && <div className="text-xs text-mute max-w-xs">{e.comentarios}</div>}</td><td className="td">{fmtFecha(e.fecha)}</td>
                  <td className="td"><Estrellas valor={e.productividad} /></td><td className="td"><Estrellas valor={e.calidad} /></td>
                  <td className="td"><Estrellas valor={e.trabajoEquipo} /></td><td className="td"><Estrellas valor={e.puntualidad} /></td>
                  <td className="td font-mono font-semibold text-fox">{e.total}</td>
                </tr>
              ))}
              {evals.data?.length === 0 && <tr><td className="td text-mute" colSpan={7}>Aún no tiene evaluaciones.</td></tr>}
            </tbody>
          </table>
        </div>
      )}

      {tab === 'formacion' && (
        <div className="card overflow-x-auto">
          <table className="w-full">
            <thead><tr><th className="th">Capacitación</th><th className="th">Proveedor</th><th className="th">Fechas</th><th className="th">Horas</th><th className="th">Estado</th><th className="th">Calificación</th></tr></thead>
            <tbody>
              {(caps.data ?? []).map((c) => (
                <tr key={c.id}>
                  <td className="td">{c.capacitacion}</td><td className="td">{c.proveedor ?? '—'}</td><td className="td text-xs">{fmtFecha(c.fechaInicio)} – {fmtFecha(c.fechaFin)}</td>
                  <td className="td">{c.horas}</td><td className="td"><EstadoBadge estado={c.estado} /></td><td className="td font-mono">{c.calificacion ?? '—'}</td>
                </tr>
              ))}
              {caps.data?.length === 0 && <tr><td className="td text-mute" colSpan={6}>Sin capacitaciones registradas.</td></tr>}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
