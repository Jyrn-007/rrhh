import { api } from '../api/client.js';
import { ejecutar } from '../api/useApi.js';
import { EstadoBadge, fmtFecha } from './ui.jsx';

/** Tabla de solicitudes con acciones de aprobar / rechazar. */
export default function AusenciasTabla({ items, onChanged, setErr }) {
  async function resolver(a, aprobar) {
    let comentario = null;
    if (aprobar) {
      if (!window.confirm(`¿Aprobar ${a.tipo.toLowerCase()} de ${a.empleado} (${a.dias} día/s)?`)) return;
    } else {
      comentario = window.prompt(`Motivo del rechazo para ${a.empleado}:`);
      if (!comentario) return;
    }
    if (await ejecutar(() => api.post(`/api/ausencias/${a.id}/resolver`, { aprobar, comentario }), setErr)) onChanged();
  }

  return (
    <div className="card overflow-x-auto">
      <table className="w-full">
        <thead><tr><th className="th">Empleado</th><th className="th">Tipo</th><th className="th">Desde</th><th className="th">Hasta</th><th className="th">Días</th><th className="th">Motivo</th><th className="th">Estado</th><th className="th" /></tr></thead>
        <tbody>
          {items.map((a) => (
            <tr key={a.id} className="hover:bg-raised/60">
              <td className="td">{a.empleado}<div className="text-xs text-mute font-mono">{a.codigo}</div></td>
              <td className="td"><EstadoBadge estado={a.tipo} /></td><td className="td">{fmtFecha(a.fechaInicio)}</td><td className="td">{fmtFecha(a.fechaFin)}</td>
              <td className="td">{a.dias}</td><td className="td text-xs text-mute max-w-[14rem]">{a.motivo}{a.comentarioResolucion && <div className="text-fox">↳ {a.comentarioResolucion}</div>}</td>
              <td className="td"><EstadoBadge estado={a.estado} /></td>
              <td className="td whitespace-nowrap text-right space-x-1">
                {a.estado === 'PENDIENTE' && (<>
                  <button className="btn btn-sm" onClick={() => resolver(a, true)}>Aprobar</button>
                  <button className="btn btn-sm btn-danger" onClick={() => resolver(a, false)}>Rechazar</button>
                </>)}
              </td>
            </tr>
          ))}
          {items.length === 0 && <tr><td className="td text-mute" colSpan={8}>Sin solicitudes.</td></tr>}
        </tbody>
      </table>
    </div>
  );
}
