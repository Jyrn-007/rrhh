import { useState } from 'react';
import { api } from '../api/client.js';
import { ejecutar, useApi } from '../api/useApi.js';
import { Alert, EstadoBadge, Field, SinEmpleado, fmtFecha, fmtHora } from '../components/ui.jsx';

const mesActual = () => {
  const h = new Date();
  const p = (n) => String(n).padStart(2, '0');
  return { desde: `${h.getFullYear()}-${p(h.getMonth() + 1)}-01`, hasta: `${h.getFullYear()}-${p(h.getMonth() + 1)}-${p(h.getDate())}` };
};

export default function MiAsistencia() {
  const [rango, setRango] = useState(mesActual());
  const { data, error, reload } = useApi('/api/asistencia/mias', rango);
  const [err, setErr] = useState('');
  const [busy, setBusy] = useState(false);

  if (error?.includes('vinculado')) return <SinEmpleado />;
  if (error) return <Alert>{error}</Alert>;
  if (!data) return <div className="text-mute">Cargando…</div>;

  const hoy = data.hoy;
  const completa = hoy?.horaEntrada && hoy?.horaSalida;

  async function marcar() {
    setBusy(true);
    await ejecutar(() => api.post('/api/asistencia/marcar'), setErr);
    await reload();
    setBusy(false);
  }

  return (
    <div className="space-y-6">
      <h1 className="text-xl font-semibold">Mi asistencia</h1>

      <section className="card p-5 max-w-xl">
        <div className="flex items-center justify-between mb-3">
          <div className="text-sm text-mute">Horario: <span className="font-mono text-ink">{data.horario.entrada} – {data.horario.salida}</span> (tolerancia {data.horario.toleranciaMin} min)</div>
          {hoy?.estado && <EstadoBadge estado={hoy.estado} />}
        </div>
        <div className="flex items-end gap-8 mb-4">
          <div><div className="text-xs text-mute">Entrada</div><div className="text-2xl font-mono">{fmtHora(hoy?.horaEntrada)}</div></div>
          <div><div className="text-xs text-mute">Salida</div><div className="text-2xl font-mono">{fmtHora(hoy?.horaSalida)}</div></div>
        </div>
        <Alert>{err}</Alert>
        {completa
          ? <div className="text-sm text-ok">Ya registró su jornada de hoy ✓</div>
          : <button className="btn btn-primary w-full mt-2" disabled={busy} onClick={marcar}>{hoy?.horaEntrada ? 'Marcar salida' : 'Marcar entrada'}</button>}
      </section>

      <div className="grid grid-cols-2 sm:grid-cols-4 gap-4 max-w-3xl">
        {[['Puntuales', data.resumen.presentes, 'text-ok'], ['Retardos', data.resumen.retardos, 'text-warn'], ['Faltas', data.resumen.faltas, 'text-bad'], ['Horas extra', data.resumen.horasExtra, 'text-fox']].map(([k, v, c]) => (
          <div key={k} className="card p-4"><div className="text-xs text-mute uppercase">{k}</div><div className={`text-2xl font-bold ${c}`}>{v}</div></div>
        ))}
      </div>

      <div className="flex flex-wrap gap-3 items-end">
        <Field label="Desde"><input type="date" className="input" value={rango.desde} onChange={(e) => setRango({ ...rango, desde: e.target.value })} /></Field>
        <Field label="Hasta"><input type="date" className="input" value={rango.hasta} onChange={(e) => setRango({ ...rango, hasta: e.target.value })} /></Field>
      </div>

      <div className="card overflow-x-auto">
        <table className="w-full">
          <thead><tr><th className="th">Fecha</th><th className="th">Entrada</th><th className="th">Salida</th><th className="th">Estado</th><th className="th">Retardo</th><th className="th">Horas</th><th className="th">Extra</th></tr></thead>
          <tbody>
            {data.items.map((a) => (
              <tr key={a.id}>
                <td className="td">{fmtFecha(a.fecha)}</td><td className="td font-mono">{fmtHora(a.horaEntrada)}</td><td className="td font-mono">{fmtHora(a.horaSalida)}</td>
                <td className="td"><EstadoBadge estado={a.estado} /></td>
                <td className="td">{a.minutosRetardo ? `${a.minutosRetardo} min` : '—'}</td><td className="td">{a.horasTrabajadas || '—'}</td><td className="td">{a.horasExtra || '—'}</td>
              </tr>
            ))}
            {data.items.length === 0 && <tr><td className="td text-mute" colSpan={7}>Sin registros en el rango.</td></tr>}
          </tbody>
        </table>
      </div>
    </div>
  );
}
