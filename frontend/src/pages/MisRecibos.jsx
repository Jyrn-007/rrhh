import { useState } from 'react';
import { useApi } from '../api/useApi.js';
import Recibo from '../components/Recibo.jsx';
import { Alert, EstadoBadge, MESES, SinEmpleado, fmtQ } from '../components/ui.jsx';

export default function MisRecibos() {
  const { data, error } = useApi('/api/nomina/mis-recibos');
  const [ver, setVer] = useState(null);

  if (error?.includes('vinculado')) return <SinEmpleado />;
  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold">Mis recibos de pago</h1>
      <Alert>{error}</Alert>
      <div className="card overflow-x-auto">
        <table className="w-full">
          <thead><tr><th className="th">Periodo</th><th className="th">Estado</th><th className="th text-right">Ingresos</th><th className="th text-right">Descuentos</th><th className="th text-right">Líquido</th><th className="th" /></tr></thead>
          <tbody>
            {(data ?? []).map((r) => (
              <tr key={r.id} className="hover:bg-raised/60">
                <td className="td">{MESES[r.mes]} {r.anio}</td><td className="td"><EstadoBadge estado={r.estado} /></td>
                <td className="td text-right font-mono">{fmtQ(r.totalDevengado)}</td><td className="td text-right font-mono">{fmtQ(r.totalDescuentos)}</td>
                <td className="td text-right font-mono font-semibold text-fox">{fmtQ(r.neto)}</td>
                <td className="td text-right"><button className="btn btn-sm" onClick={() => setVer(r.id)}>Ver recibo</button></td>
              </tr>
            ))}
            {data?.length === 0 && <tr><td className="td text-mute" colSpan={6}>Todavía no hay recibos disponibles.</td></tr>}
          </tbody>
        </table>
      </div>
      {ver && <Recibo detalleId={ver} onClose={() => setVer(null)} />}
    </div>
  );
}
