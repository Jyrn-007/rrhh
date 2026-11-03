import { useState } from 'react';
import { api } from '../api/client.js';
import { ejecutar, useApi } from '../api/useApi.js';
import { useAuth } from '../auth/AuthContext.jsx';
import { EvaluacionForm, ObjetivoForm } from '../components/EvaluacionForm.jsx';
import FormModal, { limpiar } from '../components/FormModal.jsx';
import { Alert, EstadoBadge, Estrellas, Modal, Tabs, fmtFecha } from '../components/ui.jsx';

const ESTADOS = ['PROGRAMADA', 'EN_CURSO', 'FINALIZADA', 'CANCELADA'].map((e) => ({ value: e, label: e.replaceAll('_', ' ') }));

function Participantes({ id, onClose, gestionar, empleados }) {
  const { data, error, reload } = useApi(`/api/capacitaciones/${id}`);
  const [sel, setSel] = useState('');
  const [err, setErr] = useState('');

  async function inscribir() {
    if (!sel) return;
    if (await ejecutar(() => api.post(`/api/capacitaciones/${id}/participantes`, { empleadoIds: [Number(sel)] }), setErr)) { setSel(''); reload(); }
  }
  async function actualizar(p, cambios) {
    const body = { estado: p.estado, calificacion: p.calificacion, ...cambios };
    if (body.calificacion === '' || body.calificacion === undefined) body.calificacion = null;
    if (body.calificacion !== null) body.calificacion = Number(body.calificacion);
    if (await ejecutar(() => api.put(`/api/capacitaciones/participantes/${p.id}`, body), setErr)) reload();
  }
  async function quitar(p) {
    if (!window.confirm(`¿Quitar a ${p.empleado}?`)) return;
    if (await ejecutar(() => api.delete(`/api/capacitaciones/participantes/${p.id}`), setErr)) reload();
  }

  return (
    <Modal title={data ? data.capacitacion.nombre : 'Capacitación'} onClose={onClose} wide>
      <Alert>{err || error}</Alert>
      {data && (<>
        <p className="text-sm text-mute mb-3">{fmtFecha(data.capacitacion.fechaInicio)} – {fmtFecha(data.capacitacion.fechaFin)} · {data.capacitacion.horas} h · {data.capacitacion.proveedor ?? 'Sin proveedor'}</p>
        {gestionar && (
          <div className="flex gap-2 mb-4">
            <select className="input" value={sel} onChange={(e) => setSel(e.target.value)}><option value="">Inscribir empleado…</option>{empleados.map((e) => <option key={e.id} value={e.id}>{e.nombre}</option>)}</select>
            <button className="btn btn-primary" onClick={inscribir}>Inscribir</button>
          </div>
        )}
        <table className="w-full">
          <thead><tr><th className="th">Empleado</th><th className="th">Estado</th><th className="th">Calificación</th><th className="th" /></tr></thead>
          <tbody>
            {data.participantes.map((p) => (
              <tr key={p.id}>
                <td className="td">{p.empleado}</td>
                <td className="td">{gestionar
                  ? <select className="input" value={p.estado} onChange={(e) => actualizar(p, { estado: e.target.value })}><option>INSCRITO</option><option>COMPLETADO</option><option>NO_COMPLETADO</option></select>
                  : <EstadoBadge estado={p.estado} />}</td>
                <td className="td">{gestionar
                  ? <input type="number" min="0" max="100" step="0.5" className="input max-w-[6rem]" defaultValue={p.calificacion ?? ''} onBlur={(e) => { if (String(e.target.value) !== String(p.calificacion ?? '')) actualizar(p, { calificacion: e.target.value }); }} />
                  : p.calificacion ?? '—'}</td>
                <td className="td text-right">{gestionar && <button className="btn btn-sm btn-danger" onClick={() => quitar(p)}>Quitar</button>}</td>
              </tr>
            ))}
            {data.participantes.length === 0 && <tr><td className="td text-mute" colSpan={4}>Sin participantes.</td></tr>}
          </tbody>
        </table>
      </>)}
    </Modal>
  );
}

export default function Capacitacion() {
  const { can } = useAuth();
  const gestionar = can('capacitacion.gestionar');
  const evaluar = can('evaluaciones.gestionar');
  const [tab, setTab] = useState('capacitaciones');
  const [modal, setModal] = useState(null); // 'nueva' | capacitacion | {ver:id} | 'eval' | 'obj'
  const caps = useApi('/api/capacitaciones');
  const evals = useApi('/api/desarrollo/evaluaciones', undefined, tab === 'evaluaciones');
  const objs = useApi('/api/desarrollo/objetivos', undefined, tab === 'objetivos');
  const emps = useApi('/api/empleados/lista', undefined, can('empleados.ver'));
  const listaEmps = (emps.data ?? []).map((e) => ({ id: e.id, nombre: e.nombre }));

  async function guardar(v) {
    const body = limpiar(v, ['horas']);
    body.horas = Number(v.horas || 0);
    if (modal !== 'nueva') await api.put(`/api/capacitaciones/${modal.id}`, body); else await api.post('/api/capacitaciones', body);
    setModal(null); caps.reload();
  }

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-xl font-semibold">Capacitación y evaluaciones</h1>
        <div className="flex gap-2">
          {tab === 'capacitaciones' && gestionar && <button className="btn btn-primary" onClick={() => setModal('nueva')}>+ Nueva capacitación</button>}
          {tab === 'evaluaciones' && evaluar && <button className="btn btn-primary" onClick={() => setModal('eval')}>+ Evaluación</button>}
          {tab === 'objetivos' && evaluar && <button className="btn btn-primary" onClick={() => setModal('obj')}>+ Objetivo</button>}
        </div>
      </div>
      <Tabs value={tab} onChange={setTab} tabs={[{ id: 'capacitaciones', label: 'Capacitaciones' }, { id: 'evaluaciones', label: 'Evaluaciones de desempeño' }, { id: 'objetivos', label: 'Objetivos' }]} />
      <Alert>{caps.error || evals.error || objs.error}</Alert>

      {tab === 'capacitaciones' && (
        <div className="card overflow-x-auto">
          <table className="w-full">
            <thead><tr><th className="th">Capacitación</th><th className="th">Proveedor</th><th className="th">Fechas</th><th className="th">Horas</th><th className="th">Participantes</th><th className="th">Estado</th><th className="th" /></tr></thead>
            <tbody>
              {(caps.data ?? []).map((c) => (
                <tr key={c.id} className="hover:bg-raised/60">
                  <td className="td">{c.nombre}<div className="text-xs text-mute">{c.descripcion}</div></td><td className="td">{c.proveedor ?? '—'}</td>
                  <td className="td text-xs">{fmtFecha(c.fechaInicio)} – {fmtFecha(c.fechaFin)}</td><td className="td">{c.horas}</td><td className="td">{c.participantes}</td>
                  <td className="td"><EstadoBadge estado={c.estado} /></td>
                  <td className="td whitespace-nowrap text-right space-x-1">
                    <button className="btn btn-sm" onClick={() => setModal({ ver: c.id })}>Participantes</button>
                    {gestionar && <button className="btn btn-sm" onClick={() => setModal(c)}>Editar</button>}
                  </td>
                </tr>
              ))}
              {caps.data?.length === 0 && <tr><td className="td text-mute" colSpan={7}>Sin capacitaciones registradas.</td></tr>}
            </tbody>
          </table>
        </div>
      )}

      {tab === 'evaluaciones' && (
        <div className="card overflow-x-auto">
          <table className="w-full">
            <thead><tr><th className="th">Empleado</th><th className="th">Periodo</th><th className="th">Productividad</th><th className="th">Calidad</th><th className="th">Equipo</th><th className="th">Puntualidad</th><th className="th">Total</th><th className="th">Evaluador</th></tr></thead>
            <tbody>
              {(evals.data ?? []).map((e) => (
                <tr key={e.id} className="align-top">
                  <td className="td">{e.empleado}</td><td className="td">{e.periodo}<div className="text-xs text-mute">{fmtFecha(e.fecha)}</div></td>
                  <td className="td"><Estrellas valor={e.productividad} /></td><td className="td"><Estrellas valor={e.calidad} /></td>
                  <td className="td"><Estrellas valor={e.trabajoEquipo} /></td><td className="td"><Estrellas valor={e.puntualidad} /></td>
                  <td className="td font-mono font-semibold text-fox">{e.total}</td><td className="td text-xs text-mute">{e.evaluador ?? '—'}</td>
                </tr>
              ))}
              {evals.data?.length === 0 && <tr><td className="td text-mute" colSpan={8}>Sin evaluaciones.</td></tr>}
            </tbody>
          </table>
        </div>
      )}

      {tab === 'objetivos' && (
        <div className="card overflow-x-auto">
          <table className="w-full">
            <thead><tr><th className="th">Empleado</th><th className="th">Objetivo</th><th className="th">Límite</th><th className="th">Progreso</th><th className="th">Estado</th></tr></thead>
            <tbody>
              {(objs.data ?? []).map((o) => (
                <tr key={o.id}>
                  <td className="td">{o.empleado}</td><td className="td">{o.titulo}<div className="text-xs text-mute">{o.descripcion}</div></td><td className="td">{fmtFecha(o.fechaLimite)}</td>
                  <td className="td w-40"><div className="h-2 rounded bg-canvas overflow-hidden"><div className="h-full bg-fox" style={{ width: `${o.progreso}%` }} /></div><div className="text-[10px] text-mute mt-1">{o.progreso}%</div></td>
                  <td className="td"><EstadoBadge estado={o.estado} /></td>
                </tr>
              ))}
              {objs.data?.length === 0 && <tr><td className="td text-mute" colSpan={5}>Sin objetivos.</td></tr>}
            </tbody>
          </table>
        </div>
      )}

      {(modal === 'nueva' || (modal && modal.nombre)) && (
        <FormModal title={modal === 'nueva' ? 'Nueva capacitación' : 'Editar capacitación'} onClose={() => setModal(null)} onSubmit={guardar} wide
          initial={modal === 'nueva' ? { estado: 'PROGRAMADA', horas: 0 } : modal}
          fields={[
            { k: 'nombre', label: 'Nombre', required: true, maxLength: 150, span: 2 },
            { k: 'proveedor', label: 'Proveedor / instructor', maxLength: 120 },
            { k: 'estado', label: 'Estado', type: 'select', required: true, options: ESTADOS },
            { k: 'fechaInicio', label: 'Inicio', type: 'date', required: true },
            { k: 'fechaFin', label: 'Fin', type: 'date', required: true },
            { k: 'horas', label: 'Horas', type: 'number', required: true },
            { k: 'descripcion', label: 'Descripción', type: 'textarea', maxLength: 500 },
          ]} />
      )}
      {modal?.ver && <Participantes id={modal.ver} gestionar={gestionar} empleados={listaEmps} onClose={() => { setModal(null); caps.reload(); }} />}
      {modal === 'eval' && <EvaluacionForm empleados={listaEmps} onClose={() => setModal(null)} onSaved={() => { setModal(null); evals.reload(); }} />}
      {modal === 'obj' && <ObjetivoForm empleados={listaEmps} onClose={() => setModal(null)} onSaved={() => { setModal(null); objs.reload(); }} />}
    </div>
  );
}
