import { useState } from 'react';
import { api } from '../api/client.js';
import { ejecutar, useApi } from '../api/useApi.js';
import { useAuth } from '../auth/AuthContext.jsx';
import FormModal, { limpiar } from '../components/FormModal.jsx';
import { Alert, EstadoBadge, Pager, fmtFecha, fmtQ } from '../components/ui.jsx';

const TIPOS = ['INDEFINIDO', 'PLAZO_FIJO', 'POR_OBRA', 'TEMPORAL', 'PRUEBA'].map((t) => ({ value: t, label: t.replaceAll('_', ' ') }));

export default function Contratos() {
  const { can } = useAuth();
  const gestionar = can('contratos.gestionar');
  const [q, setQ] = useState({ estado: '', porVencer: false, page: 1 });
  const [modal, setModal] = useState(null); // 'nuevo' | contrato
  const [err, setErr] = useState('');
  const pageSize = 15;
  const { data, error, reload } = useApi('/api/contratos', { estado: q.estado || undefined, porVencer: q.porVencer || undefined, page: q.page, pageSize });
  const emps = useApi('/api/empleados/lista', undefined, gestionar);

  const campos = (editando) => [
    { k: 'empleadoId', label: 'Empleado', type: 'select', required: true, span: 2, options: (emps.data ?? []).map((e) => ({ value: e.id, label: e.nombre })), disabled: editando },
    { k: 'tipo', label: 'Tipo de contrato', type: 'select', required: true, options: TIPOS },
    { k: 'salario', label: 'Salario mensual (Q)', type: 'number', step: '0.01', required: true },
    { k: 'fechaInicio', label: 'Fecha de inicio', type: 'date', required: true },
    { k: 'fechaFin', label: 'Fecha de fin', type: 'date', required: true, visible: (f) => f.tipo && f.tipo !== 'INDEFINIDO' },
    { k: 'documentoUrl', label: 'Enlace al documento firmado', maxLength: 300, span: 2 },
    { k: 'observaciones', label: 'Observaciones', type: 'textarea', maxLength: 500 },
  ];

  async function guardar(v) {
    const body = limpiar(v, ['empleadoId', 'salario']);
    if (modal !== 'nuevo') await api.put(`/api/contratos/${modal.id}`, body); else await api.post('/api/contratos', body);
    setModal(null);
    reload();
  }

  async function rescindir(c) {
    if (!window.confirm(`¿Rescindir el contrato de ${c.empleado}?`)) return;
    if (await ejecutar(() => api.post(`/api/contratos/${c.id}/rescindir`), setErr)) reload();
  }

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-xl font-semibold">Contratos</h1>
        {gestionar && <button className="btn btn-primary" onClick={() => setModal('nuevo')}>+ Nuevo contrato</button>}
      </div>
      <div className="flex flex-wrap gap-3 items-center">
        <select className="input max-w-[11rem]" value={q.estado} onChange={(e) => setQ({ ...q, estado: e.target.value, page: 1 })}>
          <option value="">Todos los estados</option>{['VIGENTE', 'VENCIDO', 'RENOVADO', 'RESCINDIDO'].map((s) => <option key={s}>{s}</option>)}
        </select>
        <label className="flex items-center gap-2 text-sm"><input type="checkbox" className="accent-orange-500" checked={q.porVencer} onChange={(e) => setQ({ ...q, porVencer: e.target.checked, page: 1 })} /> Por vencer (30 días)</label>
      </div>
      <Alert>{err || error}</Alert>
      <div className="card overflow-x-auto">
        <table className="w-full">
          <thead><tr><th className="th">Empleado</th><th className="th">Tipo</th><th className="th">Inicio</th><th className="th">Fin</th><th className="th text-right">Salario</th><th className="th">Estado</th><th className="th" /></tr></thead>
          <tbody>
            {(data?.items ?? []).map((c) => (
              <tr key={c.id} className="hover:bg-raised/60">
                <td className="td">{c.empleado}<div className="text-xs text-mute font-mono">{c.codigo}</div></td>
                <td className="td">{c.tipo.replaceAll('_', ' ')}</td><td className="td">{fmtFecha(c.fechaInicio)}</td><td className="td">{c.fechaFin ? fmtFecha(c.fechaFin) : 'Indefinido'}</td>
                <td className="td text-right font-mono">{fmtQ(c.salario)}</td><td className="td"><EstadoBadge estado={c.estado} /></td>
                <td className="td whitespace-nowrap text-right space-x-1">
                  {c.documentoUrl && <a className="btn btn-sm" href={c.documentoUrl} target="_blank" rel="noreferrer">Documento</a>}
                  {gestionar && c.estado === 'VIGENTE' && (<>
                    <button className="btn btn-sm" onClick={() => setModal(c)}>Editar</button>
                    <button className="btn btn-sm btn-danger" onClick={() => rescindir(c)}>Rescindir</button>
                  </>)}
                </td>
              </tr>
            ))}
            {data?.items.length === 0 && <tr><td className="td text-mute" colSpan={7}>Sin contratos.</td></tr>}
          </tbody>
        </table>
        <Pager page={q.page} pageSize={pageSize} total={data?.total ?? 0} onPage={(p) => setQ({ ...q, page: p })} />
      </div>
      {modal && (
        <FormModal title={modal === 'nuevo' ? 'Nuevo contrato' : 'Editar contrato'} fields={campos(modal !== 'nuevo')} initial={modal === 'nuevo' ? {} : modal}
          onClose={() => setModal(null)} onSubmit={guardar} wide>
          {modal === 'nuevo' && <p className="text-[11px] text-mute">Si el empleado ya tiene un contrato vigente, pasará a estado RENOVADO.</p>}
        </FormModal>
      )}
    </div>
  );
}
