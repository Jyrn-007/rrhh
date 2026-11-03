import { useState } from 'react';
import { useApi } from '../api/useApi.js';
import AusenciasTabla from '../components/AusenciasTabla.jsx';
import { EvaluacionForm, ObjetivoForm } from '../components/EvaluacionForm.jsx';
import { Alert, EstadoBadge, Estrellas, Tabs, fmtFecha, fmtHora } from '../components/ui.jsx';

export default function MiEquipo() {
  const [tab, setTab] = useState('equipo');
  const [err, setErr] = useState('');
  const [form, setForm] = useState(null); // 'eval' | 'obj'
  const equipo = useApi('/api/mi/equipo');
  const pend = useApi('/api/ausencias', { estado: 'PENDIENTE', pageSize: 100 });
  const hist = useApi('/api/ausencias', { pageSize: 30 }, tab === 'aprobaciones');
  const evals = useApi('/api/desarrollo/evaluaciones', undefined, tab === 'evaluaciones');
  const objs = useApi('/api/desarrollo/objetivos', undefined, tab === 'objetivos');

  const miembros = (equipo.data ?? []).map((m) => ({ id: m.id, nombre: m.nombre }));
  const nPend = pend.data?.total ?? 0;

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-xl font-semibold">Mi equipo</h1>
        <div className="flex gap-2">
          <button className="btn" disabled={!miembros.length} onClick={() => setForm('obj')}>+ Objetivo</button>
          <button className="btn btn-primary" disabled={!miembros.length} onClick={() => setForm('eval')}>+ Evaluación</button>
        </div>
      </div>
      <Tabs value={tab} onChange={setTab} tabs={[
        { id: 'equipo', label: 'Equipo' }, { id: 'aprobaciones', label: 'Aprobaciones', badge: nPend || null },
        { id: 'evaluaciones', label: 'Evaluaciones' }, { id: 'objetivos', label: 'Objetivos' },
      ]} />
      <Alert>{err || equipo.error}</Alert>

      {tab === 'equipo' && (
        <div className="card overflow-x-auto">
          <table className="w-full">
            <thead><tr><th className="th">Empleado</th><th className="th">Puesto</th><th className="th">Contacto</th><th className="th">Hoy</th><th className="th">Entrada</th><th className="th">Salida</th></tr></thead>
            <tbody>
              {(equipo.data ?? []).map((m) => (
                <tr key={m.id} className="hover:bg-raised/60">
                  <td className="td">{m.nombre}<div className="text-xs text-mute font-mono">{m.codigo}</div></td>
                  <td className="td">{m.puesto ?? '—'}<div className="text-xs text-mute">{m.departamento}</div></td>
                  <td className="td text-xs">{m.email}<div className="text-mute">{m.telefono}</div></td>
                  <td className="td">{m.estadoHoy ? <EstadoBadge estado={m.estadoHoy} /> : <span className="text-mute text-xs">Sin marcar</span>}</td>
                  <td className="td font-mono">{fmtHora(m.horaEntrada)}</td><td className="td font-mono">{fmtHora(m.horaSalida)}</td>
                </tr>
              ))}
              {equipo.data?.length === 0 && <tr><td className="td text-mute" colSpan={6}>No tiene empleados a cargo. RRHH debe asignarle personal (campo "Jefe" en la ficha del empleado).</td></tr>}
            </tbody>
          </table>
        </div>
      )}

      {tab === 'aprobaciones' && (
        <div className="space-y-6">
          <div>
            <h2 className="font-medium mb-2">Pendientes de aprobar</h2>
            <AusenciasTabla items={pend.data?.items ?? []} setErr={setErr} onChanged={() => { pend.reload(); hist.reload(); }} />
          </div>
          <div>
            <h2 className="font-medium mb-2">Historial reciente</h2>
            <AusenciasTabla items={(hist.data?.items ?? []).filter((a) => a.estado !== 'PENDIENTE')} setErr={setErr} onChanged={hist.reload} />
          </div>
        </div>
      )}

      {tab === 'evaluaciones' && (
        <div className="card overflow-x-auto">
          <table className="w-full">
            <thead><tr><th className="th">Empleado</th><th className="th">Periodo</th><th className="th">Productividad</th><th className="th">Calidad</th><th className="th">Equipo</th><th className="th">Puntualidad</th><th className="th">Total</th></tr></thead>
            <tbody>
              {(evals.data ?? []).map((e) => (
                <tr key={e.id} className="align-top">
                  <td className="td">{e.empleado}</td><td className="td">{e.periodo}<div className="text-xs text-mute">{fmtFecha(e.fecha)}</div></td>
                  <td className="td"><Estrellas valor={e.productividad} /></td><td className="td"><Estrellas valor={e.calidad} /></td>
                  <td className="td"><Estrellas valor={e.trabajoEquipo} /></td><td className="td"><Estrellas valor={e.puntualidad} /></td>
                  <td className="td font-mono font-semibold text-fox">{e.total}</td>
                </tr>
              ))}
              {evals.data?.length === 0 && <tr><td className="td text-mute" colSpan={7}>Sin evaluaciones.</td></tr>}
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
                  <td className="td">{o.empleado}</td><td className="td">{o.titulo}</td><td className="td">{fmtFecha(o.fechaLimite)}</td>
                  <td className="td w-40"><div className="h-2 rounded bg-canvas overflow-hidden"><div className="h-full bg-fox" style={{ width: `${o.progreso}%` }} /></div><div className="text-[10px] text-mute mt-1">{o.progreso}%</div></td>
                  <td className="td"><EstadoBadge estado={o.estado} /></td>
                </tr>
              ))}
              {objs.data?.length === 0 && <tr><td className="td text-mute" colSpan={5}>Sin objetivos.</td></tr>}
            </tbody>
          </table>
        </div>
      )}

      {form === 'eval' && <EvaluacionForm empleados={miembros} onClose={() => setForm(null)} onSaved={() => { setForm(null); evals.reload(); }} />}
      {form === 'obj' && <ObjetivoForm empleados={miembros} onClose={() => setForm(null)} onSaved={() => { setForm(null); objs.reload(); }} />}
    </div>
  );
}
