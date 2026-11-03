import { useState } from 'react';
import { api, descargar } from '../api/client.js';
import { ejecutar, useApi } from '../api/useApi.js';
import { useAuth } from '../auth/AuthContext.jsx';
import FormModal from '../components/FormModal.jsx';
import Recibo from '../components/Recibo.jsx';
import { Alert, EstadoBadge, MESES, fmtFecha, fmtQ } from '../components/ui.jsx';

function Detalle({ id, onBack, gestionar }) {
  const { data, error, reload } = useApi(`/api/nomina/${id}`);
  const [err, setErr] = useState('');
  const [ajuste, setAjuste] = useState(null);
  const [recibo, setRecibo] = useState(null);

  if (error) return <Alert>{error}</Alert>;
  if (!data) return <div className="text-mute">Cargando…</div>;
  const n = data.nomina;
  const borrador = n.estado === 'BORRADOR';

  async function accion(tipo) {
    const t = { cerrar: '¿Cerrar la nómina? Ya no podrá ajustarse y los empleados verán sus recibos.', pagar: '¿Marcar la nómina como pagada?', eliminar: '¿Eliminar este borrador?' }[tipo];
    if (!window.confirm(t)) return;
    const ok = await ejecutar(() => (tipo === 'eliminar' ? api.delete(`/api/nomina/${id}`) : api.post(`/api/nomina/${id}/${tipo}`)), setErr);
    if (ok) { if (tipo === 'eliminar') onBack(true); else reload(); }
  }

  async function guardarAjuste(v) {
    await api.put(`/api/nomina/detalle/${ajuste.id}`, {
      otrosBonos: Number(v.otrosBonos || 0), isr: Number(v.isr || 0), otrosDescuentos: Number(v.otrosDescuentos || 0), observaciones: v.observaciones || null,
    });
    setAjuste(null); reload();
  }

  const Th = ({ children, r }) => <th className={`th ${r ? 'text-right' : ''}`}>{children}</th>;
  const Td = ({ children, b }) => <td className={`td text-right font-mono ${b ? 'font-semibold text-fox' : ''}`}>{children}</td>;

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex items-center gap-3">
          <button className="btn btn-sm" onClick={() => onBack(false)}>← Volver</button>
          <h2 className="text-lg font-semibold">Nómina {MESES[n.mes]} {n.anio}</h2>
          <EstadoBadge estado={n.estado} />
        </div>
        <div className="flex gap-2">
          <button className="btn" onClick={() => descargar(`/api/reportes/nomina/${id}/xlsx`, undefined, `nomina-${n.anio}-${n.mes}.xlsx`).catch(() => setErr('No se pudo exportar.'))}>Excel</button>
          {gestionar && borrador && <>
            <button className="btn btn-danger" onClick={() => accion('eliminar')}>Eliminar borrador</button>
            <button className="btn btn-primary" onClick={() => accion('cerrar')}>Cerrar nómina</button>
          </>}
          {gestionar && n.estado === 'CERRADA' && <button className="btn btn-primary" onClick={() => accion('pagar')}>Marcar pagada</button>}
        </div>
      </div>
      <div className="grid grid-cols-3 gap-4 max-w-2xl">
        {[['Devengado', n.totalDevengado], ['Descuentos', n.totalDescuentos], ['Neto a pagar', n.totalNeto]].map(([k, v], i) => (
          <div key={k} className="card p-4"><div className="text-xs text-mute uppercase">{k}</div><div className={`text-xl font-bold font-mono ${i === 2 ? 'text-fox' : ''}`}>{fmtQ(v)}</div></div>
        ))}
      </div>
      <p className="text-xs text-mute">Periodo {fmtFecha(n.fechaInicio)} al {fmtFecha(n.fechaFin)}. IGSS, bonificación y factor de hora extra salen de la configuración; el ISR se captura manualmente.</p>
      <Alert>{err}</Alert>

      <div className="card overflow-x-auto">
        <table className="w-full">
          <thead><tr><Th>Empleado</Th><Th r>Salario</Th><Th r>Horas extra</Th><Th r>Bonif.</Th><Th r>Otros bonos</Th><Th r>IGSS</Th><Th r>ISR</Th><Th r>Faltas</Th><Th r>Otros desc.</Th><Th r>Neto</Th><th className="th" /></tr></thead>
          <tbody>
            {data.detalle.map((d) => (
              <tr key={d.id} className="hover:bg-raised/60">
                <td className="td">{d.empleado}<div className="text-xs text-mute font-mono">{d.codigo}</div></td>
                <Td>{fmtQ(d.salarioBase)}</Td><Td>{fmtQ(d.montoHorasExtra)}</Td><Td>{fmtQ(d.bonificacionIncentivo)}</Td><Td>{fmtQ(d.otrosBonos)}</Td>
                <Td>{fmtQ(d.igss)}</Td><Td>{fmtQ(d.isr)}</Td><Td>{fmtQ(d.descuentoFaltas)}</Td><Td>{fmtQ(d.otrosDescuentos)}</Td><Td b>{fmtQ(d.neto)}</Td>
                <td className="td whitespace-nowrap text-right space-x-1">
                  {gestionar && borrador && <button className="btn btn-sm" onClick={() => setAjuste(d)}>Ajustar</button>}
                  <button className="btn btn-sm" onClick={() => setRecibo(d.id)}>Recibo</button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {ajuste && (
        <FormModal title={`Ajustar · ${ajuste.empleado}`} onClose={() => setAjuste(null)} onSubmit={guardarAjuste} initial={ajuste}
          fields={[
            { k: 'otrosBonos', label: 'Otros bonos (Q)', type: 'number', step: '0.01' },
            { k: 'isr', label: 'ISR (Q)', type: 'number', step: '0.01' },
            { k: 'otrosDescuentos', label: 'Otros descuentos (Q)', type: 'number', step: '0.01' },
            { k: 'observaciones', label: 'Observaciones', maxLength: 300 },
          ]} />
      )}
      {recibo && <Recibo detalleId={recibo} onClose={() => setRecibo(null)} />}
    </div>
  );
}

export default function Nomina() {
  const { can } = useAuth();
  const gestionar = can('nomina.gestionar');
  const { data, error, reload } = useApi('/api/nomina');
  const [sel, setSel] = useState(null);
  const [modal, setModal] = useState(false);
  const hoy = new Date();

  async function generar(v) {
    const r = await api.post('/api/nomina/generar', { anio: Number(v.anio), mes: Number(v.mes) });
    setModal(false); reload(); setSel(r.data.id);
  }

  if (sel) return <Detalle id={sel} gestionar={gestionar} onBack={(recargar) => { setSel(null); if (recargar) reload(); else reload(); }} />;

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-xl font-semibold">Nómina</h1>
        {gestionar && <button className="btn btn-primary" onClick={() => setModal(true)}>+ Generar nómina</button>}
      </div>
      <Alert>{error}</Alert>
      <div className="card overflow-x-auto">
        <table className="w-full">
          <thead><tr><th className="th">Periodo</th><th className="th">Estado</th><th className="th">Empleados</th><th className="th text-right">Devengado</th><th className="th text-right">Descuentos</th><th className="th text-right">Neto</th><th className="th" /></tr></thead>
          <tbody>
            {(data ?? []).map((n) => (
              <tr key={n.id} className="hover:bg-raised/60 cursor-pointer" onClick={() => setSel(n.id)}>
                <td className="td">{MESES[n.mes]} {n.anio}</td><td className="td"><EstadoBadge estado={n.estado} /></td><td className="td">{n.empleados}</td>
                <td className="td text-right font-mono">{fmtQ(n.totalDevengado)}</td><td className="td text-right font-mono">{fmtQ(n.totalDescuentos)}</td>
                <td className="td text-right font-mono font-semibold text-fox">{fmtQ(n.totalNeto)}</td><td className="td text-right"><button className="btn btn-sm">Abrir</button></td>
              </tr>
            ))}
            {data?.length === 0 && <tr><td className="td text-mute" colSpan={7}>Aún no se ha generado ninguna nómina.</td></tr>}
          </tbody>
        </table>
      </div>
      {modal && (
        <FormModal title="Generar nómina mensual" onClose={() => setModal(false)} onSubmit={generar} submitLabel="Generar borrador" initial={{ anio: hoy.getFullYear(), mes: hoy.getMonth() + 1 }}
          fields={[
            { k: 'mes', label: 'Mes', type: 'select', required: true, options: MESES.slice(1).map((m, i) => ({ value: i + 1, label: m })) },
            { k: 'anio', label: 'Año', type: 'number', required: true },
          ]}>
          <p className="text-xs text-mute">Se calcula con el contrato vigente de cada empleado activo, las faltas y las horas extra registradas en el mes. Podrá revisar y ajustar antes de cerrarla.</p>
        </FormModal>
      )}
    </div>
  );
}
