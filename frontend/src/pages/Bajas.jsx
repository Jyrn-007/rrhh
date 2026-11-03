import { useState } from 'react';
import { api } from '../api/client.js';
import { ejecutar, useApi } from '../api/useApi.js';
import { useAuth } from '../auth/AuthContext.jsx';
import FormModal from '../components/FormModal.jsx';
import { Alert, Field, Modal, fmtFecha, fmtQ } from '../components/ui.jsx';

const TIPOS = [
  ['RENUNCIA', 'Renuncia'], ['DESPIDO_JUSTIFICADO', 'Despido justificado'], ['DESPIDO_INJUSTIFICADO', 'Despido injustificado (con indemnización)'],
  ['FIN_CONTRATO', 'Fin de contrato'], ['MUTUO_ACUERDO', 'Mutuo acuerdo'],
];
const tipoTxt = (t) => TIPOS.find((x) => x[0] === t)?.[1] ?? t;
const hoy = () => new Date().toISOString().substring(0, 10);

function Desglose({ c, otros = 0, desc = 0, total }) {
  const filas = [
    ['Indemnización', c.indemnizacion], ['Aguinaldo proporcional', c.aguinaldo], ['Bono 14 proporcional', c.bono14],
    [`Vacaciones pendientes (${c.vacacionesDias} días)`, c.vacacionesMonto], ['Salario pendiente del mes', c.salarioPendiente], ['Otros ingresos', otros],
  ];
  return (
    <table className="w-full text-sm">
      <tbody>
        {filas.map(([k, v]) => <tr key={k}><td className="py-1">{k}</td><td className="py-1 text-right font-mono">{fmtQ(v)}</td></tr>)}
        <tr><td className="py-1">Descuentos</td><td className="py-1 text-right font-mono text-bad">-{fmtQ(desc)}</td></tr>
        <tr className="border-t border-line"><td className="py-2 font-semibold">Total a pagar</td><td className="py-2 text-right font-mono font-bold text-fox">{fmtQ(total)}</td></tr>
      </tbody>
    </table>
  );
}

function NuevaBaja({ empleados, onClose, onSaved }) {
  const [f, setF] = useState({ empleadoId: '', tipo: 'RENUNCIA', fechaBaja: hoy(), motivo: '', observaciones: '', otrosIngresos: 0, descuentos: 0 });
  const [prev, setPrev] = useState(null);
  const [err, setErr] = useState('');
  const [busy, setBusy] = useState(false);
  const set = (k) => (e) => { setF({ ...f, [k]: e.target.value }); setPrev(null); };
  const valido = f.empleadoId && f.fechaBaja;

  async function calcular() {
    setBusy(true);
    await ejecutar(async () => {
      const r = await api.post('/api/bajas/calcular', { empleadoId: Number(f.empleadoId), tipo: f.tipo, fechaBaja: f.fechaBaja });
      setPrev(r.data);
    }, setErr);
    setBusy(false);
  }
  async function registrar() {
    if (!window.confirm('Se dará de baja al empleado y se desactivará su usuario. ¿Continuar?')) return;
    setBusy(true);
    const ok = await ejecutar(() => api.post('/api/bajas', {
      empleadoId: Number(f.empleadoId), tipo: f.tipo, fechaBaja: f.fechaBaja, motivo: f.motivo || null, observaciones: f.observaciones || null,
      otrosIngresos: Number(f.otrosIngresos || 0), descuentos: Number(f.descuentos || 0),
    }), setErr);
    setBusy(false);
    if (ok) onSaved();
  }

  return (
    <Modal title="Registrar baja y liquidación" onClose={onClose} wide>
      <Alert>{err}</Alert>
      <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
        <Field label="Empleado"><select className="input" value={f.empleadoId} onChange={set('empleadoId')}><option value="">Seleccione…</option>{empleados.map((e) => <option key={e.id} value={e.id}>{e.nombre}</option>)}</select></Field>
        <Field label="Tipo de baja"><select className="input" value={f.tipo} onChange={set('tipo')}>{TIPOS.map(([v, l]) => <option key={v} value={v}>{l}</option>)}</select></Field>
        <Field label="Fecha de baja"><input type="date" className="input" value={f.fechaBaja} onChange={set('fechaBaja')} /></Field>
        <Field label="Motivo"><input className="input" maxLength={300} value={f.motivo} onChange={set('motivo')} /></Field>
        <Field label="Otros ingresos (Q)"><input type="number" step="0.01" className="input" value={f.otrosIngresos} onChange={set('otrosIngresos')} /></Field>
        <Field label="Descuentos (Q)"><input type="number" step="0.01" className="input" value={f.descuentos} onChange={set('descuentos')} /></Field>
        <Field label="Observaciones" className="md:col-span-2"><textarea className="input" maxLength={500} value={f.observaciones} onChange={set('observaciones')} /></Field>
      </div>
      <div className="mt-4"><button className="btn" disabled={!valido || busy} onClick={calcular}>Calcular liquidación</button></div>
      {prev && (
        <div className="card p-4 mt-4">
          <Desglose c={prev.calculo} otros={Number(f.otrosIngresos || 0)} desc={Number(f.descuentos || 0)}
            total={prev.total + Number(f.otrosIngresos || 0) - Number(f.descuentos || 0)} />
          <p className="text-xs text-mute mt-2">Cálculo estimado: valide con contabilidad / asesoría laboral antes de pagar.</p>
        </div>
      )}
      <div className="flex justify-end gap-2 mt-4">
        <button className="btn" onClick={onClose}>Cancelar</button>
        <button className="btn btn-danger" disabled={!prev || busy} onClick={registrar}>Confirmar baja</button>
      </div>
    </Modal>
  );
}

function VerBaja({ id, onClose, gestionar, onChanged }) {
  const { data, error, reload } = useApi(`/api/bajas/${id}`);
  const [aj, setAj] = useState(false);
  const l = data?.liquidacion;

  async function guardar(v) {
    await api.put(`/api/bajas/${id}/liquidacion`, { otrosIngresos: Number(v.otrosIngresos || 0), descuentos: Number(v.descuentos || 0), observaciones: v.observaciones || null });
    setAj(false); reload(); onChanged();
  }

  return (
    <Modal title="Liquidación" onClose={onClose} wide>
      <Alert>{error}</Alert>
      {data && l && (<>
        <div id="imprimible" className="card p-6 text-sm">
          <div className="text-lg font-bold text-fox">{data.empresa}</div>
          <div className="text-xs text-mute mb-3">Liquidación laboral · {tipoTxt(data.baja.tipo)}</div>
          <div className="grid grid-cols-2 gap-x-6 gap-y-1 text-xs mb-4">
            <div><span className="text-mute">Empleado: </span>{data.empleado.nombre} ({data.empleado.codigo})</div>
            <div><span className="text-mute">DPI: </span>{data.empleado.dpi ?? '—'}</div>
            <div><span className="text-mute">Puesto: </span>{data.empleado.puesto ?? '—'}</div>
            <div><span className="text-mute">Departamento: </span>{data.empleado.departamento ?? '—'}</div>
            <div><span className="text-mute">Ingreso: </span>{fmtFecha(data.empleado.fechaIngreso)}</div>
            <div><span className="text-mute">Baja: </span>{fmtFecha(data.baja.fechaBaja)}</div>
            <div><span className="text-mute">Salario base: </span>{fmtQ(l.salarioBase)}</div>
            <div><span className="text-mute">Años de servicio: </span>{l.aniosServicio}</div>
            {data.baja.motivo && <div className="col-span-2"><span className="text-mute">Motivo: </span>{data.baja.motivo}</div>}
          </div>
          <Desglose c={{ indemnizacion: l.indemnizacion, aguinaldo: l.aguinaldo, bono14: l.bono14, vacacionesDias: l.vacacionesDias, vacacionesMonto: l.vacacionesMonto, salarioPendiente: l.salarioPendiente }}
            otros={l.otrosIngresos} desc={l.descuentos} total={l.total} />
          {l.observaciones && <p className="text-xs text-mute mt-3">Obs.: {l.observaciones}</p>}
          <div className="grid grid-cols-2 gap-12 mt-14 text-xs text-center"><div className="border-t border-line pt-1">Firma del empleado</div><div className="border-t border-line pt-1">Representante de la empresa</div></div>
        </div>
        <div className="flex justify-end gap-2 mt-4">
          {gestionar && <button className="btn" onClick={() => setAj(true)}>Ajustar montos</button>}
          <button className="btn btn-primary" onClick={() => window.print()}>Imprimir / PDF</button>
        </div>
        {aj && (
          <FormModal title="Ajustar liquidación" onClose={() => setAj(false)} onSubmit={guardar} initial={l}
            fields={[
              { k: 'otrosIngresos', label: 'Otros ingresos (Q)', type: 'number', step: '0.01' },
              { k: 'descuentos', label: 'Descuentos (Q)', type: 'number', step: '0.01' },
              { k: 'observaciones', label: 'Observaciones', maxLength: 500 },
            ]} />
        )}
      </>)}
    </Modal>
  );
}

export default function Bajas() {
  const { can } = useAuth();
  const gestionar = can('bajas.gestionar');
  const { data, error, reload } = useApi('/api/bajas');
  const emps = useApi('/api/empleados/lista', undefined, gestionar);
  const [modal, setModal] = useState(null);
  const activos = (emps.data ?? []).filter((e) => e.activo !== false).map((e) => ({ id: e.id, nombre: e.nombre }));

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-xl font-semibold">Bajas y liquidaciones</h1>
        {gestionar && <button className="btn btn-primary" onClick={() => setModal('nueva')}>+ Registrar baja</button>}
      </div>
      <Alert>{error}</Alert>
      <div className="card overflow-x-auto">
        <table className="w-full">
          <thead><tr><th className="th">Empleado</th><th className="th">Tipo</th><th className="th">Fecha</th><th className="th">Motivo</th><th className="th text-right">Liquidación</th><th className="th" /></tr></thead>
          <tbody>
            {(data ?? []).map((b) => (
              <tr key={b.id} className="hover:bg-raised/60">
                <td className="td">{b.empleado}<div className="text-xs text-mute font-mono">{b.codigo}</div></td><td className="td">{tipoTxt(b.tipo)}</td><td className="td">{fmtFecha(b.fechaBaja)}</td>
                <td className="td text-xs">{b.motivo ?? '—'}</td><td className="td text-right font-mono font-semibold text-fox">{fmtQ(b.total)}</td>
                <td className="td text-right"><button className="btn btn-sm" onClick={() => setModal({ ver: b.id })}>Ver</button></td>
              </tr>
            ))}
            {data?.length === 0 && <tr><td className="td text-mute" colSpan={6}>No hay bajas registradas.</td></tr>}
          </tbody>
        </table>
      </div>
      {modal === 'nueva' && <NuevaBaja empleados={activos} onClose={() => setModal(null)} onSaved={() => { setModal(null); reload(); emps.reload(); }} />}
      {modal?.ver && <VerBaja id={modal.ver} gestionar={gestionar} onClose={() => setModal(null)} onChanged={reload} />}
    </div>
  );
}
