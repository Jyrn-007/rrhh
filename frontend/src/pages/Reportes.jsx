import { useState } from 'react';
import { descargar } from '../api/client.js';
import { Alert, Field } from '../components/ui.jsx';

const REPORTES = [
  { id: 'empleados', titulo: 'Empleados', desc: 'Listado completo con puesto, departamento, estado y salario.', fechas: false },
  { id: 'asistencia', titulo: 'Asistencia', desc: 'Marcas de entrada/salida, tardanzas, faltas y horas extra.', fechas: true },
  { id: 'ausencias', titulo: 'Vacaciones, permisos y ausencias', desc: 'Solicitudes y su estado de aprobación.', fechas: true },
  { id: 'contratos', titulo: 'Contratos', desc: 'Contratos vigentes, vencidos y por vencer.', fechas: false },
  { id: 'bajas', titulo: 'Bajas y liquidaciones', desc: 'Terminaciones laborales con montos de liquidación.', fechas: true },
  { id: 'capacitaciones', titulo: 'Capacitaciones', desc: 'Cursos, participantes y calificaciones.', fechas: true },
];

export default function Reportes() {
  const hoy = new Date();
  const [desde, setDesde] = useState(new Date(hoy.getFullYear(), hoy.getMonth(), 1).toISOString().substring(0, 10));
  const [hasta, setHasta] = useState(hoy.toISOString().substring(0, 10));
  const [busy, setBusy] = useState('');
  const [err, setErr] = useState('');

  async function bajar(r) {
    setBusy(r.id); setErr('');
    try { await descargar(`/api/reportes/${r.id}.xlsx`, r.fechas ? { desde, hasta } : undefined, `${r.id}.xlsx`); }
    catch { setErr('No se pudo generar el reporte.'); }
    finally { setBusy(''); }
  }

  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold">Reportes</h1>
      <div className="card p-4 flex flex-wrap items-end gap-4">
        <Field label="Desde"><input type="date" className="input" value={desde} onChange={(e) => setDesde(e.target.value)} /></Field>
        <Field label="Hasta"><input type="date" className="input" value={hasta} onChange={(e) => setHasta(e.target.value)} /></Field>
        <p className="text-xs text-mute max-w-sm">El rango aplica a los reportes con fechas. La nómina se exporta desde su propio detalle.</p>
      </div>
      <Alert>{err}</Alert>
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {REPORTES.map((r) => (
          <div key={r.id} className="card p-4 flex flex-col">
            <div className="font-semibold">{r.titulo}</div>
            <p className="text-xs text-mute flex-1 mt-1">{r.desc}</p>
            <button className="btn btn-primary mt-3" disabled={busy === r.id} onClick={() => bajar(r)}>{busy === r.id ? 'Generando…' : 'Descargar Excel'}</button>
          </div>
        ))}
      </div>
    </div>
  );
}
