import { useState } from 'react';
import { api } from '../api/client.js';
import { ejecutar, useApi } from '../api/useApi.js';
import { useAuth } from '../auth/AuthContext.jsx';
import FormModal, { limpiar, opciones } from '../components/FormModal.jsx';
import { Alert, Badge, Tabs, fmtQ } from '../components/ui.jsx';

export default function Organizacion() {
  const { can } = useAuth();
  const gestionar = can('organizacion.gestionar');
  const [tab, setTab] = useState('departamentos');
  const [modal, setModal] = useState(null); // { tipo, item }
  const [err, setErr] = useState('');
  const deps = useApi('/api/organizacion/departamentos');
  const puestos = useApi('/api/organizacion/puestos');
  const sucs = useApi('/api/organizacion/sucursales');
  const emps = useApi('/api/empleados/lista', undefined, can('empleados.ver'));

  const recargar = () => { deps.reload(); puestos.reload(); sucs.reload(); };
  const base = '/api/organizacion';

  async function estado(ruta, item) {
    if (!window.confirm(`¿${item.activo ? 'Desactivar' : 'Activar'} "${item.nombre}"?`)) return;
    if (await ejecutar(() => api.post(`${base}/${ruta}/${item.id}/estado`, { activo: !item.activo }), setErr)) recargar();
  }

  async function guardar(ruta, item, valores, numericos) {
    const body = limpiar(valores, numericos);
    if (item) await api.put(`${base}/${ruta}/${item.id}`, body); else await api.post(`${base}/${ruta}`, body);
    setModal(null);
    recargar();
  }

  const camposDep = [
    { k: 'nombre', label: 'Nombre', required: true, maxLength: 100 },
    { k: 'jefeEmpleadoId', label: 'Jefe del departamento', type: 'select', options: (emps.data ?? []).map((e) => ({ value: e.id, label: e.nombre })) },
    { k: 'departamentoPadreId', label: 'Departamento padre', type: 'select', options: opciones((deps.data ?? []).filter((d) => d.activo)) },
    { k: 'descripcion', label: 'Descripción', maxLength: 250 },
  ];
  const camposPuesto = [
    { k: 'nombre', label: 'Nombre', required: true, maxLength: 100 },
    { k: 'departamentoId', label: 'Departamento', type: 'select', required: true, options: opciones((deps.data ?? []).filter((d) => d.activo)) },
    { k: 'salarioMinimo', label: 'Salario mínimo (Q)', type: 'number', step: '0.01' },
    { k: 'salarioMaximo', label: 'Salario máximo (Q)', type: 'number', step: '0.01' },
  ];
  const camposSuc = [
    { k: 'nombre', label: 'Nombre', required: true, maxLength: 100 },
    { k: 'telefono', label: 'Teléfono', maxLength: 30 },
    { k: 'direccion', label: 'Dirección', maxLength: 250, span: 2 },
  ];

  const Acciones = ({ ruta, tipo, item }) => gestionar && (
    <td className="td whitespace-nowrap text-right space-x-1">
      <button className="btn btn-sm" onClick={() => setModal({ tipo, item })}>Editar</button>
      <button className={`btn btn-sm ${item.activo ? 'btn-danger' : ''}`} onClick={() => estado(ruta, item)}>{item.activo ? 'Desactivar' : 'Activar'}</button>
    </td>
  );
  const Est = ({ a }) => <Badge tone={a ? 'ok' : 'bad'}>{a ? 'Activo' : 'Inactivo'}</Badge>;

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-xl font-semibold">Organización</h1>
        {gestionar && <button className="btn btn-primary" onClick={() => setModal({ tipo: tab, item: null })}>
          + Nuevo {tab === 'departamentos' ? 'departamento' : tab === 'puestos' ? 'puesto' : 'sucursal'}</button>}
      </div>
      <Tabs value={tab} onChange={setTab} tabs={[{ id: 'departamentos', label: 'Departamentos' }, { id: 'puestos', label: 'Puestos' }, { id: 'sucursales', label: 'Sucursales' }]} />
      <Alert>{err}</Alert>

      <div className="card overflow-x-auto">
        {tab === 'departamentos' && (
          <table className="w-full">
            <thead><tr><th className="th">Departamento</th><th className="th">Jefe</th><th className="th">Padre</th><th className="th">Empleados</th><th className="th">Estado</th><th className="th" /></tr></thead>
            <tbody>
              {(deps.data ?? []).map((d) => (
                <tr key={d.id} className="hover:bg-raised/60">
                  <td className="td">{d.nombre}<div className="text-xs text-mute">{d.descripcion}</div></td><td className="td">{d.jefe ?? '—'}</td>
                  <td className="td">{(deps.data ?? []).find((x) => x.id === d.departamentoPadreId)?.nombre ?? '—'}</td>
                  <td className="td">{d.empleados}</td><td className="td"><Est a={d.activo} /></td><Acciones ruta="departamentos" tipo="departamentos" item={d} />
                </tr>
              ))}
            </tbody>
          </table>
        )}
        {tab === 'puestos' && (
          <table className="w-full">
            <thead><tr><th className="th">Puesto</th><th className="th">Departamento</th><th className="th">Rango salarial</th><th className="th">Empleados</th><th className="th">Estado</th><th className="th" /></tr></thead>
            <tbody>
              {(puestos.data ?? []).map((p) => (
                <tr key={p.id} className="hover:bg-raised/60">
                  <td className="td">{p.nombre}</td><td className="td">{p.departamento}</td>
                  <td className="td text-xs">{p.salarioMinimo != null || p.salarioMaximo != null ? `${fmtQ(p.salarioMinimo)} – ${fmtQ(p.salarioMaximo)}` : '—'}</td>
                  <td className="td">{p.empleados}</td><td className="td"><Est a={p.activo} /></td><Acciones ruta="puestos" tipo="puestos" item={p} />
                </tr>
              ))}
            </tbody>
          </table>
        )}
        {tab === 'sucursales' && (
          <table className="w-full">
            <thead><tr><th className="th">Sucursal</th><th className="th">Dirección</th><th className="th">Teléfono</th><th className="th">Empleados</th><th className="th">Estado</th><th className="th" /></tr></thead>
            <tbody>
              {(sucs.data ?? []).map((s) => (
                <tr key={s.id} className="hover:bg-raised/60">
                  <td className="td">{s.nombre}</td><td className="td text-xs">{s.direccion ?? '—'}</td><td className="td">{s.telefono ?? '—'}</td>
                  <td className="td">{s.empleados}</td><td className="td"><Est a={s.activo} /></td><Acciones ruta="sucursales" tipo="sucursales" item={s} />
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {modal?.tipo === 'departamentos' && (
        <FormModal title={modal.item ? 'Editar departamento' : 'Nuevo departamento'} fields={camposDep} initial={modal.item ?? {}} onClose={() => setModal(null)}
          onSubmit={(v) => guardar('departamentos', modal.item, v, ['jefeEmpleadoId', 'departamentoPadreId'])} />
      )}
      {modal?.tipo === 'puestos' && (
        <FormModal title={modal.item ? 'Editar puesto' : 'Nuevo puesto'} fields={camposPuesto} initial={modal.item ?? {}} onClose={() => setModal(null)}
          onSubmit={(v) => guardar('puestos', modal.item, v, ['departamentoId', 'salarioMinimo', 'salarioMaximo'])} />
      )}
      {modal?.tipo === 'sucursales' && (
        <FormModal title={modal.item ? 'Editar sucursal' : 'Nueva sucursal'} fields={camposSuc} initial={modal.item ?? {}} onClose={() => setModal(null)}
          onSubmit={(v) => guardar('sucursales', modal.item, v, [])} />
      )}
    </div>
  );
}
