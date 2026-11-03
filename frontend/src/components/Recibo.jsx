import { useEffect, useState } from 'react';
import { api, errMsg } from '../api/client.js';
import { Alert, MESES, Modal, fmtFecha, fmtQ } from './ui.jsx';

const Fila = ({ k, v, neg }) => (
  <tr><td className="py-1 pr-4">{k}</td><td className={`py-1 text-right font-mono ${neg ? 'text-bad' : ''}`}>{neg ? '-' : ''}{fmtQ(v)}</td></tr>
);

/** Recibo de pago imprimible (Imprimir → Guardar como PDF). */
export default function Recibo({ detalleId, onClose }) {
  const [r, setR] = useState(null);
  const [error, setError] = useState('');

  useEffect(() => {
    api.get(`/api/nomina/recibo/${detalleId}`).then((x) => setR(x.data)).catch((e) => setError(errMsg(e)));
  }, [detalleId]);

  return (
    <Modal title="Recibo de pago" onClose={onClose} wide>
      <Alert>{error}</Alert>
      {r && (
        <>
          <div id="imprimible" className="card p-6 text-sm">
            <div className="flex justify-between items-start mb-4">
              <div>
                <div className="text-lg font-bold text-fox">{r.empresa}</div>
                <div className="text-xs text-mute">Recibo de pago · {MESES[r.periodo.mes]} {r.periodo.anio}</div>
                <div className="text-xs text-mute">Periodo: {fmtFecha(r.periodo.fechaInicio)} al {fmtFecha(r.periodo.fechaFin)}</div>
              </div>
              <div className="text-right text-xs"><div className="font-mono">{r.empleado.codigo}</div><div>{r.periodo.estado}</div></div>
            </div>
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-2 mb-4 text-xs border-y border-line py-3">
              <div><span className="text-mute">Empleado: </span>{r.empleado.nombre}</div>
              <div><span className="text-mute">DPI: </span>{r.empleado.dpi}</div>
              <div><span className="text-mute">Puesto: </span>{r.empleado.puesto ?? '—'}</div>
              <div><span className="text-mute">Departamento: </span>{r.empleado.departamento ?? '—'}</div>
            </div>
            <div className="grid sm:grid-cols-2 gap-6">
              <div>
                <div className="text-xs uppercase tracking-wide text-mute mb-1">Ingresos</div>
                <table className="w-full"><tbody>
                  <Fila k="Salario base" v={r.detalle.salarioBase} />
                  <Fila k={`Horas extra (${r.detalle.horasExtra})`} v={r.detalle.montoHorasExtra} />
                  <Fila k="Bonificación incentivo" v={r.detalle.bonificacionIncentivo} />
                  <Fila k="Otros bonos" v={r.detalle.otrosBonos} />
                  <tr className="border-t border-line font-semibold"><td className="pt-2">Total ingresos</td><td className="pt-2 text-right font-mono">{fmtQ(r.detalle.totalDevengado)}</td></tr>
                </tbody></table>
              </div>
              <div>
                <div className="text-xs uppercase tracking-wide text-mute mb-1">Descuentos</div>
                <table className="w-full"><tbody>
                  <Fila k="IGSS laboral" v={r.detalle.igss} neg />
                  <Fila k="ISR" v={r.detalle.isr} neg />
                  <Fila k={`Faltas (${r.detalle.diasFaltados} día/s)`} v={r.detalle.descuentoFaltas} neg />
                  <Fila k="Otros descuentos" v={r.detalle.otrosDescuentos} neg />
                  <tr className="border-t border-line font-semibold"><td className="pt-2">Total descuentos</td><td className="pt-2 text-right font-mono">-{fmtQ(r.detalle.totalDescuentos)}</td></tr>
                </tbody></table>
              </div>
            </div>
            <div className="mt-5 flex justify-between items-center border-t border-line pt-3">
              <div className="font-semibold">Líquido a recibir</div>
              <div className="text-2xl font-bold text-fox font-mono">{fmtQ(r.detalle.neto)}</div>
            </div>
            {r.detalle.observaciones && <div className="mt-3 text-xs text-mute">Observaciones: {r.detalle.observaciones}</div>}
          </div>
          <div className="flex justify-end gap-2 mt-4 no-print">
            <button className="btn" onClick={onClose}>Cerrar</button>
            <button className="btn btn-primary" onClick={() => window.print()}>Imprimir / PDF</button>
          </div>
        </>
      )}
    </Modal>
  );
}
