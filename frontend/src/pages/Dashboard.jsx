import { useState } from 'react';
import { Link } from 'react-router-dom';
import { api } from '../api/client.js';
import { ejecutar, useApi } from '../api/useApi.js';
import { useAuth } from '../auth/AuthContext.jsx';
import { Alert, EstadoBadge, fmtFecha, fmtHora } from '../components/ui.jsx';

function Stat({ label, value, tone = 'text-ink', to }) {
  const c = (
    <div className={`card p-4 h-full ${to ? 'hover:border-fox transition' : ''}`}>
      <div className="text-xs text-mute uppercase tracking-wide">{label}</div>
      <div className={`text-3xl font-bold mt-1 ${tone}`}>{value}</div>
    </div>
  );
  return to ? <Link to={to}>{c}</Link> : c;
}

function MiResumen({ r, reload }) {
  const { can } = useAuth();
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const a = r.asistenciaHoy;
  const completa = a?.horaEntrada && a?.horaSalida;

  async function marcar() {
    setBusy(true);
    await ejecutar(() => api.post('/api/asistencia/marcar'), setError);
    await reload();
    setBusy(false);
  }

  return (
    <section className="space-y-4">
      <div className="flex flex-wrap items-end justify-between gap-2">
        <div>
          <h1 className="text-xl font-semibold">Hola, {r.empleado.nombre.split(' ')[0]}</h1>
          <p className="text-sm text-mute">{[r.empleado.puesto, r.empleado.departamento].filter(Boolean).join(' · ')}</p>
        </div>
      </div>
      <Alert>{error}</Alert>
      <div className="grid lg:grid-cols-3 gap-4">
        {can('asistencia.marcar') && (
          <div className="card p-5 lg:col-span-1">
            <div className="text-xs text-mute uppercase tracking-wide mb-2">Asistencia de hoy</div>
            <div className="flex items-center gap-4 text-sm mb-4">
              <div><div className="text-mute text-xs">Entrada</div><div className="text-lg font-mono">{fmtHora(a?.horaEntrada)}</div></div>
              <div><div className="text-mute text-xs">Salida</div><div className="text-lg font-mono">{fmtHora(a?.horaSalida)}</div></div>
              {a?.estado && <EstadoBadge estado={a.estado} />}
            </div>
            <div className="text-xs text-mute mb-3">Horario: {r.horario.entrada} – {r.horario.salida}</div>
            {completa
              ? <div className="text-sm text-ok">Jornada registrada ✓</div>
              : <button className="btn btn-primary w-full" disabled={busy} onClick={marcar}>{a?.horaEntrada ? 'Marcar salida' : 'Marcar entrada'}</button>}
          </div>
        )}
        <div className="lg:col-span-2 grid grid-cols-2 sm:grid-cols-4 gap-4">
          {r.vacaciones && <Stat label="Vacaciones disp." value={r.vacaciones.disponible} tone="text-fox" to="/mi/solicitudes" />}
          <Stat label="Solicitudes pendientes" value={r.solicitudesPendientes} tone="text-warn" to={can('ausencias.solicitar') ? '/mi/solicitudes' : undefined} />
          <Stat label="Objetivos activos" value={r.objetivosActivos} to={can('desarrollo.propio') ? '/mi/desarrollo' : undefined} />
          <Stat label="Capacitaciones" value={r.capacitacionesProximas} to={can('desarrollo.propio') ? '/mi/desarrollo' : undefined} />
        </div>
      </div>

      {r.equipo && (
        <div>
          <h2 className="font-medium mb-3">Mi equipo hoy</h2>
          <div className="grid grid-cols-2 lg:grid-cols-5 gap-4">
            <Stat label="Miembros" value={r.equipo.miembros} to="/equipo" />
            <Stat label="Presentes" value={r.equipo.presentes} tone="text-ok" />
            <Stat label="Retardos" value={r.equipo.retardos} tone="text-warn" />
            <Stat label="Ausentes" value={r.equipo.ausentes} tone="text-mute" />
            <Stat label="Por aprobar" value={r.equipo.pendientesAprobacion} tone="text-fox" to="/equipo" />
          </div>
        </div>
      )}
    </section>
  );
}

function OrgStats() {
  const { data: d, error } = useApi('/api/dashboard');
  if (error) return <Alert>{error}</Alert>;
  if (!d) return <div className="text-mute">Cargando…</div>;
  const max = Math.max(1, ...d.empleadosPorDepartamento.map((x) => x.total));

  return (
    <section className="space-y-6">
      <h2 className="text-lg font-semibold">Organización</h2>
      <div className="grid grid-cols-2 lg:grid-cols-5 gap-4">
        <Stat label="Total empleados" value={d.totalEmpleados} />
        <Stat label="Activos" value={d.empleadosActivos} tone="text-ok" />
        <Stat label="Inactivos" value={d.empleadosInactivos} tone="text-mute" />
        <Stat label="Ausentes hoy" value={d.ausentesHoy} tone="text-warn" />
        <Stat label="Solicitudes pendientes" value={d.solicitudesPendientes} tone="text-fox" />
      </div>
      <div className="grid lg:grid-cols-2 gap-6">
        <div className="card p-5">
          <h3 className="font-medium mb-4">Empleados por departamento</h3>
          {d.empleadosPorDepartamento.length === 0 && <p className="text-sm text-mute">Sin datos todavía.</p>}
          <div className="space-y-3">
            {d.empleadosPorDepartamento.map((x) => (
              <div key={x.departamento}>
                <div className="flex justify-between text-xs mb-1"><span>{x.departamento}</span><span className="text-mute">{x.total}</span></div>
                <div className="h-2 rounded bg-canvas overflow-hidden">
                  <div className="h-full rounded bg-fox" style={{ width: `${(x.total / max) * 100}%`, boxShadow: '0 0 10px rgba(255,122,26,.6)' }} />
                </div>
              </div>
            ))}
          </div>
        </div>
        <div className="card p-5">
          <h3 className="font-medium mb-4">Cumpleaños del mes</h3>
          {d.cumpleanosDelMes.length === 0 && <p className="text-sm text-mute">Nadie cumple años este mes.</p>}
          <ul className="space-y-2 text-sm">
            {d.cumpleanosDelMes.map((c) => (
              <li key={c.id} className="flex justify-between border-b border-line/60 pb-2"><span>{c.nombre}</span><span className="text-fox font-mono">día {c.dia}</span></li>
            ))}
          </ul>
        </div>
        <div className="card p-5 lg:col-span-2">
          <h3 className="font-medium mb-4">Contratos próximos a vencer (30 días)</h3>
          {d.contratosPorVencer.length === 0 && <p className="text-sm text-mute">No hay contratos por vencer.</p>}
          <ul className="space-y-2 text-sm">
            {d.contratosPorVencer.map((c) => (
              <li key={c.id} className="flex justify-between border-b border-line/60 pb-2">
                <span>{c.empleado} <span className="text-mute">· {c.tipo}</span></span><span className="text-warn font-mono">{fmtFecha(c.fechaFin)}</span>
              </li>
            ))}
          </ul>
        </div>
      </div>
    </section>
  );
}

export default function Dashboard() {
  const { can } = useAuth();
  const { data: r, error, reload } = useApi('/api/mi/resumen');

  return (
    <div className="space-y-8">
      <Alert>{error}</Alert>
      {r?.vinculado && <MiResumen r={r} reload={reload} />}
      {r && !r.vinculado && !can('empleados.ver') && (
        <div className="card p-6 max-w-xl">
          <h2 className="font-semibold mb-2">Bienvenido</h2>
          <p className="text-sm text-mute">Su usuario aún no está vinculado a un empleado. Pida a un administrador que lo vincule para ver su asistencia, solicitudes y recibos.</p>
        </div>
      )}
      {can('empleados.ver') && <OrgStats />}
    </div>
  );
}
