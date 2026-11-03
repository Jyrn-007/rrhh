import { useState } from 'react';
import { api } from '../api/client.js';
import { ejecutar, useApi } from '../api/useApi.js';
import { Alert, EstadoBadge, Field, SinEmpleado, fmtFecha } from '../components/ui.jsx';

function diasHabiles(ini, fin) {
  if (!ini || !fin || fin < ini) return 0;
  let n = 0;
  const d = new Date(`${ini}T12:00:00`);
  const f = new Date(`${fin}T12:00:00`);
  while (d <= f) { const w = d.getDay(); if (w !== 0 && w !== 6) n++; d.setDate(d.getDate() + 1); }
  return n;
}

export default function MisSolicitudes() {
  const saldo = useApi('/api/ausencias/saldo');
  const mias = useApi('/api/ausencias/mias');
  const [f, setF] = useState({ tipo: 'VACACIONES', fechaInicio: '', fechaFin: '', motivo: '' });
  const [err, setErr] = useState('');
  const [ok, setOk] = useState('');

  if (saldo.error?.includes('vinculado')) return <SinEmpleado />;
  const set = (k) => (e) => setF((p) => ({ ...p, [k]: e.target.value }));
  const dias = diasHabiles(f.fechaInicio, f.fechaFin);

  async function enviar(e) {
    e.preventDefault();
    setOk('');
    const body = { ...f, motivo: f.motivo || null };
    if (await ejecutar(() => api.post('/api/ausencias', body), setErr)) {
      setOk('Solicitud enviada. Su supervisor o RRHH la revisará.');
      setF({ tipo: 'VACACIONES', fechaInicio: '', fechaFin: '', motivo: '' });
      saldo.reload(); mias.reload();
    }
  }

  async function cancelar(id) {
    if (!window.confirm('¿Cancelar esta solicitud?')) return;
    if (await ejecutar(() => api.post(`/api/ausencias/${id}/cancelar`), setErr)) { saldo.reload(); mias.reload(); }
  }

  const s = saldo.data;
  return (
    <div className="space-y-6">
      <h1 className="text-xl font-semibold">Mis solicitudes</h1>

      {s && (
        <div className="grid grid-cols-2 sm:grid-cols-5 gap-4 max-w-4xl">
          {[['Años de servicio', s.anios], ['Derecho', s.derecho], ['Gozados', s.usados], ['En trámite', s.pendientes], ['Disponibles', s.disponible]].map(([k, v], i) => (
            <div key={k} className="card p-4"><div className="text-xs text-mute uppercase">{k}</div><div className={`text-2xl font-bold ${i === 4 ? 'text-fox' : ''}`}>{v}</div></div>
          ))}
        </div>
      )}

      <form onSubmit={enviar} className="card p-5 max-w-3xl space-y-3">
        <h2 className="font-medium">Nueva solicitud</h2>
        <Alert>{err}</Alert>
        <Alert tone="ok">{ok}</Alert>
        <div className="grid sm:grid-cols-4 gap-3">
          <Field label="Tipo">
            <select className="input" value={f.tipo} onChange={set('tipo')}><option value="VACACIONES">Vacaciones</option><option value="PERMISO">Permiso</option><option value="INCAPACIDAD">Incapacidad</option></select>
          </Field>
          <Field label="Desde"><input type="date" className="input" value={f.fechaInicio} onChange={set('fechaInicio')} required /></Field>
          <Field label="Hasta"><input type="date" className="input" value={f.fechaFin} onChange={set('fechaFin')} required /></Field>
          <div className="flex items-end text-sm text-mute pb-2">{dias > 0 ? `${dias} día(s) hábil(es)` : ''}</div>
        </div>
        <Field label="Motivo"><input className="input" value={f.motivo} onChange={set('motivo')} maxLength={300} /></Field>
        <div className="flex justify-end"><button className="btn btn-primary">Enviar solicitud</button></div>
      </form>

      <div className="card overflow-x-auto">
        <table className="w-full">
          <thead><tr><th className="th">Tipo</th><th className="th">Desde</th><th className="th">Hasta</th><th className="th">Días</th><th className="th">Estado</th><th className="th">Resolución</th><th className="th" /></tr></thead>
          <tbody>
            {(mias.data ?? []).map((a) => (
              <tr key={a.id}>
                <td className="td"><EstadoBadge estado={a.tipo} /></td><td className="td">{fmtFecha(a.fechaInicio)}</td><td className="td">{fmtFecha(a.fechaFin)}</td><td className="td">{a.dias}</td>
                <td className="td"><EstadoBadge estado={a.estado} /></td><td className="td text-xs text-mute">{a.comentarioResolucion}</td>
                <td className="td text-right">{(a.estado === 'PENDIENTE' || a.estado === 'APROBADA') && <button className="btn btn-sm btn-danger" onClick={() => cancelar(a.id)}>Cancelar</button>}</td>
              </tr>
            ))}
            {mias.data?.length === 0 && <tr><td className="td text-mute" colSpan={7}>Aún no ha enviado solicitudes.</td></tr>}
          </tbody>
        </table>
      </div>
    </div>
  );
}
