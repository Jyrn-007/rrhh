import { useCallback, useEffect, useState } from 'react';
import { api, errMsg } from '../api/client.js';
import { useAuth } from '../auth/AuthContext.jsx';
import { Alert, Badge, Field, Modal, Pager, fmtFecha } from '../components/ui.jsx';

const VACIO = {
  nombres: '', apellidos: '', dpi: '', nit: '', fechaNacimiento: '', genero: '', estadoCivil: '', email: '', telefono: '',
  direccion: '', contactoEmergenciaNombre: '', contactoEmergenciaTelefono: '', contactoEmergenciaParentesco: '',
  fechaIngreso: '', departamentoId: '', puestoId: '', sucursalId: '', jefeId: '', horarioId: '',
};

function EmpleadoForm({ id, onClose, onSaved }) {
  const [f, setF] = useState(VACIO);
  const [deps, setDeps] = useState([]);
  const [puestos, setPuestos] = useState([]);
  const [sucs, setSucs] = useState([]);
  const [jefes, setJefes] = useState([]);
  const [horarios, setHorarios] = useState([]);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    api.get('/api/catalogos/departamentos').then((r) => setDeps(r.data));
    api.get('/api/catalogos/sucursales').then((r) => setSucs(r.data));
    api.get('/api/empleados/lista').then((r) => setJefes(r.data)).catch(() => {});
    api.get('/api/asistencia/horarios').then((r) => setHorarios(r.data)).catch(() => {});
    if (id) {
      api.get(`/api/empleados/${id}`).then((r) => {
        const e = r.data;
        const n = { ...VACIO };
        Object.keys(VACIO).forEach((k) => { n[k] = e[k] ?? ''; });
        setF(n);
      }).catch((e) => setError(errMsg(e)));
    }
  }, [id]);

  useEffect(() => {
    api.get('/api/catalogos/puestos', { params: { departamentoId: f.departamentoId || undefined } }).then((r) => setPuestos(r.data));
  }, [f.departamentoId]);

  const set = (k) => (e) => setF((p) => ({ ...p, [k]: e.target.value }));

  async function submit(e) {
    e.preventDefault();
    setBusy(true);
    setError('');
    const body = {};
    Object.entries(f).forEach(([k, v]) => { body[k] = v === '' ? null : v; });
    ['departamentoId', 'puestoId', 'sucursalId', 'jefeId', 'horarioId'].forEach((k) => { if (body[k] !== null) body[k] = Number(body[k]); });
    try {
      if (id) await api.put(`/api/empleados/${id}`, body);
      else await api.post('/api/empleados', body);
      onSaved();
    } catch (err) {
      setError(errMsg(err));
    } finally {
      setBusy(false);
    }
  }

  return (
    <Modal title={id ? 'Editar empleado' : 'Nuevo empleado'} onClose={onClose} wide>
      <form onSubmit={submit} className="space-y-4">
        <Alert>{error}</Alert>
        <div className="grid sm:grid-cols-2 gap-3">
          <Field label="Nombres *"><input className="input" value={f.nombres} onChange={set('nombres')} required maxLength={100} /></Field>
          <Field label="Apellidos *"><input className="input" value={f.apellidos} onChange={set('apellidos')} required maxLength={100} /></Field>
          <Field label="DPI * (13 dígitos)"><input className="input font-mono" value={f.dpi} onChange={set('dpi')} required pattern="\d{13}" maxLength={13} /></Field>
          <Field label="NIT"><input className="input font-mono" value={f.nit} onChange={set('nit')} maxLength={15} /></Field>
          <Field label="Fecha de nacimiento *"><input type="date" className="input" value={f.fechaNacimiento} onChange={set('fechaNacimiento')} required /></Field>
          <Field label="Fecha de ingreso *"><input type="date" className="input" value={f.fechaIngreso} onChange={set('fechaIngreso')} required /></Field>
          <Field label="Género">
            <select className="input" value={f.genero} onChange={set('genero')}><option value="">—</option><option value="M">Masculino</option><option value="F">Femenino</option><option value="O">Otro</option></select>
          </Field>
          <Field label="Estado civil">
            <select className="input" value={f.estadoCivil} onChange={set('estadoCivil')}><option value="">—</option><option>Soltero/a</option><option>Casado/a</option><option>Unido/a</option><option>Divorciado/a</option><option>Viudo/a</option></select>
          </Field>
          <Field label="Correo"><input type="email" className="input" value={f.email} onChange={set('email')} maxLength={150} /></Field>
          <Field label="Teléfono"><input className="input" value={f.telefono} onChange={set('telefono')} maxLength={30} /></Field>
          <Field label="Dirección" className="sm:col-span-2"><input className="input" value={f.direccion} onChange={set('direccion')} maxLength={250} /></Field>
        </div>

        <h3 className="text-xs uppercase tracking-wide text-fox pt-2">Contacto de emergencia</h3>
        <div className="grid sm:grid-cols-3 gap-3">
          <Field label="Nombre"><input className="input" value={f.contactoEmergenciaNombre} onChange={set('contactoEmergenciaNombre')} maxLength={120} /></Field>
          <Field label="Teléfono"><input className="input" value={f.contactoEmergenciaTelefono} onChange={set('contactoEmergenciaTelefono')} maxLength={30} /></Field>
          <Field label="Parentesco"><input className="input" value={f.contactoEmergenciaParentesco} onChange={set('contactoEmergenciaParentesco')} maxLength={50} /></Field>
        </div>

        <h3 className="text-xs uppercase tracking-wide text-fox pt-2">Ubicación en la organización</h3>
        <div className="grid sm:grid-cols-3 gap-3">
          <Field label="Departamento">
            <select className="input" value={f.departamentoId} onChange={(e) => setF((p) => ({ ...p, departamentoId: e.target.value, puestoId: '' }))}>
              <option value="">—</option>{deps.map((d) => <option key={d.id} value={d.id}>{d.nombre}</option>)}
            </select>
          </Field>
          <Field label="Puesto">
            <select className="input" value={f.puestoId} onChange={set('puestoId')}>
              <option value="">—</option>{puestos.map((p) => <option key={p.id} value={p.id}>{p.nombre}</option>)}
            </select>
          </Field>
          <Field label="Sucursal">
            <select className="input" value={f.sucursalId} onChange={set('sucursalId')}>
              <option value="">—</option>{sucs.map((s) => <option key={s.id} value={s.id}>{s.nombre}</option>)}
            </select>
          </Field>
        </div>
        <div className="grid sm:grid-cols-2 gap-3">
          <Field label="Jefe inmediato">
            <select className="input" value={f.jefeId} onChange={set('jefeId')}>
              <option value="">—</option>{jefes.filter((j) => j.id !== id).map((j) => <option key={j.id} value={j.id}>{j.nombre}</option>)}
            </select>
          </Field>
          <Field label="Horario">
            <select className="input" value={f.horarioId} onChange={set('horarioId')}>
              <option value="">—</option>{horarios.map((h) => <option key={h.id} value={h.id}>{h.nombre}</option>)}
            </select>
          </Field>
        </div>

        <div className="flex justify-end gap-2 pt-2">
          <button type="button" className="btn" onClick={onClose}>Cancelar</button>
          <button className="btn btn-primary" disabled={busy}>{busy ? 'Guardando…' : 'Guardar'}</button>
        </div>
      </form>
    </Modal>
  );
}

export default function Empleados() {
  const { can } = useAuth();
  const [data, setData] = useState({ items: [], total: 0 });
  const [q, setQ] = useState({ buscar: '', activo: '', page: 1 });
  const [error, setError] = useState('');
  const [form, setForm] = useState(null); // null | 'nuevo' | id
  const pageSize = 15;

  const cargar = useCallback(() => {
    api.get('/api/empleados', { params: { buscar: q.buscar || undefined, activo: q.activo === '' ? undefined : q.activo, page: q.page, pageSize } })
      .then((r) => setData(r.data)).catch((e) => setError(errMsg(e)));
  }, [q]);

  useEffect(() => {
    const t = setTimeout(cargar, 250);
    return () => clearTimeout(t);
  }, [cargar]);

  async function cambiarEstado(e) {
    const accion = e.activo ? 'desactivar' : 'activar';
    if (!window.confirm(`¿Seguro que desea ${accion} a ${e.nombres} ${e.apellidos}?${e.activo ? '\nTambién se desactivará su usuario del sistema.' : ''}`)) return;
    try {
      await api.post(`/api/empleados/${e.id}/estado`, { activo: !e.activo });
      cargar();
    } catch (err) { setError(errMsg(err)); }
  }

  async function exportar() {
    try {
      const r = await api.get('/api/reportes/empleados.xlsx', { responseType: 'blob' });
      const url = URL.createObjectURL(r.data);
      const a = document.createElement('a');
      a.href = url;
      a.download = 'empleados.xlsx';
      a.click();
      URL.revokeObjectURL(url);
    } catch (err) { setError(errMsg(err)); }
  }

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-xl font-semibold">Empleados</h1>
        <div className="flex gap-2">
          {can('reportes.ver') && <button className="btn" onClick={exportar}>Exportar Excel</button>}
          {can('empleados.crear') && <button className="btn btn-primary" onClick={() => setForm('nuevo')}>+ Nuevo empleado</button>}
        </div>
      </div>

      <div className="flex flex-wrap gap-3">
        <input className="input max-w-xs" placeholder="Buscar nombre, código o DPI…" value={q.buscar} onChange={(e) => setQ({ ...q, buscar: e.target.value, page: 1 })} />
        <select className="input max-w-[10rem]" value={q.activo} onChange={(e) => setQ({ ...q, activo: e.target.value, page: 1 })}>
          <option value="">Todos</option><option value="true">Activos</option><option value="false">Inactivos</option>
        </select>
      </div>

      <Alert>{error}</Alert>

      <div className="card overflow-x-auto">
        <table className="w-full">
          <thead><tr>
            <th className="th">Código</th><th className="th">Nombre</th><th className="th">Departamento</th><th className="th">Puesto</th>
            <th className="th">Ingreso</th><th className="th">Estado</th><th className="th" />
          </tr></thead>
          <tbody>
            {data.items.map((e) => (
              <tr key={e.id} className="hover:bg-raised/60">
                <td className="td font-mono text-mute">{e.codigo}</td>
                <td className="td">{e.apellidos}, {e.nombres}<div className="text-xs text-mute">{e.email}</div></td>
                <td className="td">{e.departamento ?? '—'}</td>
                <td className="td">{e.puesto ?? '—'}</td>
                <td className="td">{fmtFecha(e.fechaIngreso)}</td>
                <td className="td"><Badge tone={e.activo ? 'ok' : 'bad'}>{e.activo ? e.estadoLaboral : (e.estadoLaboral === 'RETIRADO' ? 'RETIRADO' : 'INACTIVO')}</Badge></td>
                <td className="td whitespace-nowrap text-right space-x-2">
                  {can('empleados.editar') && <button className="btn btn-sm" onClick={() => setForm(e.id)}>Editar</button>}
                  {can('empleados.estado') && <button className={`btn btn-sm ${e.activo ? 'btn-danger' : ''}`} onClick={() => cambiarEstado(e)}>{e.activo ? 'Desactivar' : 'Activar'}</button>}
                </td>
              </tr>
            ))}
            {data.items.length === 0 && <tr><td className="td text-mute" colSpan={7}>Sin resultados.</td></tr>}
          </tbody>
        </table>
        <Pager page={q.page} pageSize={pageSize} total={data.total} onPage={(p) => setQ({ ...q, page: p })} />
      </div>

      {form && <EmpleadoForm id={form === 'nuevo' ? null : form} onClose={() => setForm(null)} onSaved={() => { setForm(null); cargar(); }} />}
    </div>
  );
}
