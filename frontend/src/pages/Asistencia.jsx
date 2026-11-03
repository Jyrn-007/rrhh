import { useState } from 'react';
import { api } from '../api/client.js';
import { useApi } from '../api/useApi.js';
import { useAuth } from '../auth/AuthContext.jsx';
import FormModal, { limpiar } from '../components/FormModal.jsx';
import { Alert, Badge, EstadoBadge, Field, Pager, Tabs, fmtFecha, fmtHora } from '../components/ui.jsx';

const hoyIso = () => { const d = new Date(); const p = (n) => String(n).padStart(2, '0'); return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}`; };
const DIAS = { 1: 'L', 2: 'M', 3: 'X', 4: 'J', 5: 'V', 6: 'S', 7: 'D' };

export default function Asistencia() {
  const { can } = useAuth();
  const gestionar = can('asistencia.gestionar');
  const [tab, setTab] = useState('registros');
  const [q, setQ] = useState({ desde: hoyIso(), hasta: hoyIso(), empleadoId: '', estado: '', page: 1 });
  const [modal, setModal] = useState(null); // 'manual' | 'faltas' | 'horario' | horario
  const [msg, setMsg] = useState('');
  const pageSize = 30;
  const regs = useApi('/api/asistencia', { desde: q.desde, hasta: q.hasta, empleadoId: q.empleadoId || undefined, estado: q.estado || undefined, page: q.page, pageSize });
  const horarios = useApi('/api/asistencia/horarios');
  const emps = useApi('/api/empleados/lista', undefined, can('empleados.ver'));
  const optEmps = (emps.data ?? []).map((e) => ({ value: e.id, label: e.nombre }));

  async function manual(v) {
    await api.post('/api/asistencia/manual', limpiar(v, ['empleadoId']));
    setModal(null); regs.reload();
  }
  async function faltas(v) {
    const r = await api.post('/api/asistencia/generar-faltas', { fecha: v.fecha });
    setModal(null); setMsg(`Se generaron ${r.data.creadas} falta(s) para ${fmtFecha(v.fecha)}.`); regs.reload();
  }
  async function guardarHorario(v) {
    const body = { ...limpiar(v, ['toleranciaMin']), activo: !!v.activo };
    if (modal !== 'horario') await api.put(`/api/asistencia/horarios/${modal.id}`, body); else await api.post('/api/asistencia/horarios', body);
    setModal(null); horarios.reload();
  }

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-xl font-semibold">Asistencia y horarios</h1>
        {gestionar && (
          <div className="flex gap-2">
            {tab === 'registros' && <>
              <button className="btn" onClick={() => setModal('faltas')}>Generar faltas</button>
              <button className="btn btn-primary" onClick={() => setModal('manual')}>+ Registro manual</button>
            </>}
            {tab === 'horarios' && <button className="btn btn-primary" onClick={() => setModal('horario')}>+ Nuevo horario</button>}
          </div>
        )}
      </div>
      <Tabs value={tab} onChange={setTab} tabs={[{ id: 'registros', label: 'Registros' }, { id: 'horarios', label: 'Horarios' }]} />
      <Alert tone="ok">{msg}</Alert>
      <Alert>{regs.error || horarios.error}</Alert>

      {tab === 'registros' && (<>
        <div className="flex flex-wrap gap-3 items-end">
          <Field label="Desde"><input type="date" className="input" value={q.desde} onChange={(e) => setQ({ ...q, desde: e.target.value, page: 1 })} /></Field>
          <Field label="Hasta"><input type="date" className="input" value={q.hasta} onChange={(e) => setQ({ ...q, hasta: e.target.value, page: 1 })} /></Field>
          <Field label="Empleado"><select className="input max-w-[16rem]" value={q.empleadoId} onChange={(e) => setQ({ ...q, empleadoId: e.target.value, page: 1 })}><option value="">Todos</option>{optEmps.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}</select></Field>
          <Field label="Estado"><select className="input" value={q.estado} onChange={(e) => setQ({ ...q, estado: e.target.value, page: 1 })}><option value="">Todos</option><option>PRESENTE</option><option>RETARDO</option><option>FALTA</option></select></Field>
        </div>
        <div className="card overflow-x-auto">
          <table className="w-full">
            <thead><tr><th className="th">Fecha</th><th className="th">Empleado</th><th className="th">Entrada</th><th className="th">Salida</th><th className="th">Estado</th><th className="th">Retardo</th><th className="th">Horas</th><th className="th">Extra</th><th className="th">Origen</th></tr></thead>
            <tbody>
              {(regs.data?.items ?? []).map((a) => (
                <tr key={a.id} className="hover:bg-raised/60">
                  <td className="td">{fmtFecha(a.fecha)}</td><td className="td">{a.empleado}<div className="text-xs text-mute font-mono">{a.codigo}</div></td>
                  <td className="td font-mono">{fmtHora(a.horaEntrada)}</td><td className="td font-mono">{fmtHora(a.horaSalida)}</td><td className="td"><EstadoBadge estado={a.estado} /></td>
                  <td className="td">{a.minutosRetardo ? `${a.minutosRetardo} min` : '—'}</td><td className="td">{a.horasTrabajadas || '—'}</td><td className="td">{a.horasExtra || '—'}</td>
                  <td className="td text-xs text-mute">{a.origen}{a.observaciones && <div title={a.observaciones}>✎ nota</div>}</td>
                </tr>
              ))}
              {regs.data?.items.length === 0 && <tr><td className="td text-mute" colSpan={9}>Sin registros en el rango.</td></tr>}
            </tbody>
          </table>
          <Pager page={q.page} pageSize={pageSize} total={regs.data?.total ?? 0} onPage={(p) => setQ({ ...q, page: p })} />
        </div>
      </>)}

      {tab === 'horarios' && (
        <div className="card overflow-x-auto">
          <table className="w-full">
            <thead><tr><th className="th">Horario</th><th className="th">Entrada</th><th className="th">Salida</th><th className="th">Tolerancia</th><th className="th">Días</th><th className="th">Estado</th><th className="th" /></tr></thead>
            <tbody>
              {(horarios.data ?? []).map((h) => (
                <tr key={h.id}>
                  <td className="td">{h.nombre}</td><td className="td font-mono">{h.horaEntrada}</td><td className="td font-mono">{h.horaSalida}</td><td className="td">{h.toleranciaMin} min</td>
                  <td className="td font-mono">{h.diasLaborales.split(',').map((d) => DIAS[d]).join(' ')}</td>
                  <td className="td"><Badge tone={h.activo ? 'ok' : 'bad'}>{h.activo ? 'Activo' : 'Inactivo'}</Badge></td>
                  <td className="td text-right">{gestionar && <button className="btn btn-sm" onClick={() => setModal(h)}>Editar</button>}</td>
                </tr>
              ))}
            </tbody>
          </table>
          <p className="text-[11px] text-mute p-3">El horario de cada empleado se asigna en su ficha. Sin horario asignado se usa el primero activo. Días: 1=lunes … 7=domingo.</p>
        </div>
      )}

      {modal === 'manual' && (
        <FormModal title="Registro / corrección manual" onClose={() => setModal(null)} onSubmit={manual} initial={{ fecha: hoyIso() }}
          fields={[
            { k: 'empleadoId', label: 'Empleado', type: 'select', required: true, span: 2, options: optEmps },
            { k: 'fecha', label: 'Fecha', type: 'date', required: true },
            { k: 'horaEntrada', label: 'Hora de entrada', type: 'time', required: true },
            { k: 'horaSalida', label: 'Hora de salida', type: 'time' },
            { k: 'observaciones', label: 'Motivo de la corrección', maxLength: 300, span: 2 },
          ]} />
      )}
      {modal === 'faltas' && (
        <FormModal title="Generar faltas del día" onClose={() => setModal(null)} onSubmit={faltas} submitLabel="Generar" fields={[{ k: 'fecha', label: 'Fecha (anterior a hoy)', type: 'date', required: true }]}>
          <p className="text-xs text-mute">Se marcará FALTA a los empleados activos sin registro de asistencia ese día laborable y sin ausencia aprobada.</p>
        </FormModal>
      )}
      {(modal === 'horario' || (modal && modal.diasLaborales)) && (
        <FormModal title={modal === 'horario' ? 'Nuevo horario' : 'Editar horario'} onClose={() => setModal(null)} onSubmit={guardarHorario}
          initial={modal === 'horario' ? { horaEntrada: '08:00', horaSalida: '17:00', toleranciaMin: 10, diasLaborales: '1,2,3,4,5', activo: true } : modal}
          fields={[
            { k: 'nombre', label: 'Nombre', required: true, maxLength: 80, span: 2 },
            { k: 'horaEntrada', label: 'Entrada', type: 'time', required: true },
            { k: 'horaSalida', label: 'Salida', type: 'time', required: true },
            { k: 'toleranciaMin', label: 'Tolerancia (min)', type: 'number', required: true },
            { k: 'diasLaborales', label: 'Días laborales', required: true, hint: '1=lunes … 7=domingo, separados por coma' },
            { k: 'activo', label: 'Activo', type: 'checkbox' },
          ]} />
      )}
    </div>
  );
}
