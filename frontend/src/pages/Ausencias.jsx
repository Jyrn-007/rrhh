import { useState } from 'react';
import { api } from '../api/client.js';
import { useApi } from '../api/useApi.js';
import { useAuth } from '../auth/AuthContext.jsx';
import AusenciasTabla from '../components/AusenciasTabla.jsx';
import FormModal, { limpiar } from '../components/FormModal.jsx';
import { Alert, Pager } from '../components/ui.jsx';

export default function Ausencias() {
  const { can } = useAuth();
  const gestionar = can('ausencias.gestionar');
  const [q, setQ] = useState({ estado: 'PENDIENTE', empleadoId: '', page: 1 });
  const [modal, setModal] = useState(false);
  const [err, setErr] = useState('');
  const pageSize = 20;
  const { data, error, reload } = useApi('/api/ausencias', { estado: q.estado || undefined, empleadoId: q.empleadoId || undefined, page: q.page, pageSize });
  const emps = useApi('/api/empleados/lista', undefined, can('empleados.ver'));
  const optEmps = (emps.data ?? []).map((e) => ({ value: e.id, label: e.nombre }));

  async function registrar(v) {
    await api.post('/api/ausencias/registrar', limpiar(v, ['empleadoId']));
    setModal(false); reload();
  }

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-xl font-semibold">Vacaciones, permisos e incapacidades</h1>
        {gestionar && <button className="btn btn-primary" onClick={() => setModal(true)}>+ Registrar ausencia</button>}
      </div>
      <div className="flex flex-wrap gap-3">
        <select className="input max-w-[12rem]" value={q.estado} onChange={(e) => setQ({ ...q, estado: e.target.value, page: 1 })}>
          <option value="">Todos los estados</option>{['PENDIENTE', 'APROBADA', 'RECHAZADA', 'CANCELADA'].map((s) => <option key={s}>{s}</option>)}
        </select>
        <select className="input max-w-[16rem]" value={q.empleadoId} onChange={(e) => setQ({ ...q, empleadoId: e.target.value, page: 1 })}>
          <option value="">Todos los empleados</option>{optEmps.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}
        </select>
      </div>
      <Alert>{err || error}</Alert>
      <AusenciasTabla items={data?.items ?? []} onChanged={reload} setErr={setErr} />
      <div className="card"><Pager page={q.page} pageSize={pageSize} total={data?.total ?? 0} onPage={(p) => setQ({ ...q, page: p })} /></div>

      {modal && (
        <FormModal title="Registrar ausencia (queda aprobada)" onClose={() => setModal(false)} onSubmit={registrar}
          fields={[
            { k: 'empleadoId', label: 'Empleado', type: 'select', required: true, span: 2, options: optEmps },
            { k: 'tipo', label: 'Tipo', type: 'select', required: true, options: [{ value: 'INCAPACIDAD', label: 'Incapacidad' }, { value: 'PERMISO', label: 'Permiso' }, { value: 'VACACIONES', label: 'Vacaciones' }] },
            { k: 'fechaInicio', label: 'Desde', type: 'date', required: true },
            { k: 'fechaFin', label: 'Hasta', type: 'date', required: true },
            { k: 'motivo', label: 'Motivo / referencia (ej. constancia IGSS)', maxLength: 300, span: 2 },
          ]} />
      )}
    </div>
  );
}
