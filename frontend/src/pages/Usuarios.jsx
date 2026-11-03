import { useCallback, useEffect, useState } from 'react';
import { api, errMsg } from '../api/client.js';
import { useAuth } from '../auth/AuthContext.jsx';
import { Alert, Badge, Field, Modal, Pager, fmtFechaHora } from '../components/ui.jsx';

function generarPassword() {
  const sets = ['ABCDEFGHJKLMNPQRSTUVWXYZ', 'abcdefghijkmnpqrstuvwxyz', '23456789', '!@#$%*?'];
  const all = sets.join('');
  const rnd = (n) => { const a = new Uint32Array(1); crypto.getRandomValues(a); return a[0] % n; };
  const out = sets.map((s) => s[rnd(s.length)]);
  while (out.length < 14) out.push(all[rnd(all.length)]);
  return out.sort(() => rnd(3) - 1).join('');
}

function NuevoUsuario({ onClose, onSaved }) {
  const [roles, setRoles] = useState([]);
  const [emps, setEmps] = useState([]);
  const [f, setF] = useState({ username: '', email: '', rolId: '', empleadoId: '', passwordTemporal: generarPassword() });
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    api.get('/api/catalogos/roles').then((r) => setRoles(r.data));
    api.get('/api/empleados', { params: { activo: true, pageSize: 100 } }).then((r) => setEmps(r.data.items)).catch(() => {});
  }, []);

  const set = (k) => (e) => setF((p) => ({ ...p, [k]: e.target.value }));

  async function submit(e) {
    e.preventDefault();
    setBusy(true);
    setError('');
    try {
      await api.post('/api/usuarios', { ...f, rolId: Number(f.rolId), empleadoId: f.empleadoId ? Number(f.empleadoId) : null });
      onSaved(f.passwordTemporal);
    } catch (err) { setError(errMsg(err)); } finally { setBusy(false); }
  }

  return (
    <Modal title="Nuevo usuario" onClose={onClose}>
      <form onSubmit={submit} className="space-y-3">
        <Alert>{error}</Alert>
        <Field label="Usuario *"><input className="input" value={f.username} onChange={set('username')} required minLength={3} maxLength={50} pattern="[a-zA-Z0-9._\-]+" /></Field>
        <Field label="Correo *"><input type="email" className="input" value={f.email} onChange={set('email')} required /></Field>
        <Field label="Rol *">
          <select className="input" value={f.rolId} onChange={set('rolId')} required><option value="">—</option>{roles.map((r) => <option key={r.id} value={r.id}>{r.nombre}</option>)}</select>
        </Field>
        <Field label="Empleado vinculado">
          <select className="input" value={f.empleadoId} onChange={set('empleadoId')}><option value="">— ninguno —</option>{emps.map((x) => <option key={x.id} value={x.id}>{x.apellidos}, {x.nombres}</option>)}</select>
        </Field>
        <Field label="Contraseña temporal (se pedirá cambiarla al ingresar)">
          <div className="flex gap-2">
            <input className="input font-mono" value={f.passwordTemporal} onChange={set('passwordTemporal')} required />
            <button type="button" className="btn" onClick={() => setF((p) => ({ ...p, passwordTemporal: generarPassword() }))}>↻</button>
          </div>
        </Field>
        <div className="flex justify-end gap-2 pt-2">
          <button type="button" className="btn" onClick={onClose}>Cancelar</button>
          <button className="btn btn-primary" disabled={busy}>{busy ? 'Creando…' : 'Crear usuario'}</button>
        </div>
      </form>
    </Modal>
  );
}

function EditarUsuario({ u, onClose, onSaved }) {
  const [roles, setRoles] = useState([]);
  const [emps, setEmps] = useState([]);
  const [f, setF] = useState({ email: u.email ?? '', rolId: u.rolId ?? '', empleadoId: u.empleadoId ?? '' });
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    api.get('/api/catalogos/roles').then((r) => setRoles(r.data));
    api.get('/api/empleados/lista').then((r) => setEmps(r.data)).catch(() => {});
  }, []);
  const set = (k) => (e) => setF((p) => ({ ...p, [k]: e.target.value }));

  async function submit(e) {
    e.preventDefault();
    setBusy(true); setError('');
    try {
      await api.put(`/api/usuarios/${u.id}`, { email: f.email, rolId: Number(f.rolId), empleadoId: f.empleadoId ? Number(f.empleadoId) : null });
      onSaved();
    } catch (err) { setError(errMsg(err)); } finally { setBusy(false); }
  }

  return (
    <Modal title={`Editar ${u.username}`} onClose={onClose}>
      <form onSubmit={submit} className="space-y-3">
        <Alert>{error}</Alert>
        <Field label="Correo *"><input type="email" className="input" value={f.email} onChange={set('email')} required /></Field>
        <Field label="Rol *"><select className="input" value={f.rolId} onChange={set('rolId')} required><option value="">—</option>{roles.map((r) => <option key={r.id} value={r.id}>{r.nombre}</option>)}</select></Field>
        <Field label="Empleado vinculado"><select className="input" value={f.empleadoId} onChange={set('empleadoId')}><option value="">— ninguno —</option>{emps.map((x) => <option key={x.id} value={x.id}>{x.nombre}</option>)}</select></Field>
        <p className="text-xs text-mute">Si cambia el rol o el empleado vinculado, el usuario debe volver a iniciar sesión.</p>
        <div className="flex justify-end gap-2 pt-2">
          <button type="button" className="btn" onClick={onClose}>Cancelar</button>
          <button className="btn btn-primary" disabled={busy}>{busy ? 'Guardando…' : 'Guardar'}</button>
        </div>
      </form>
    </Modal>
  );
}

export default function Usuarios() {
  const { can, user } = useAuth();
  const gestionar = can('usuarios.gestionar');
  const [data, setData] = useState({ items: [], total: 0 });
  const [q, setQ] = useState({ buscar: '', page: 1 });
  const [error, setError] = useState('');
  const [nuevo, setNuevo] = useState(false);
  const [editar, setEditar] = useState(null);
  const [secreto, setSecreto] = useState(null); // { titulo, password }
  const pageSize = 15;

  const cargar = useCallback(() => {
    api.get('/api/usuarios', { params: { buscar: q.buscar || undefined, page: q.page, pageSize } })
      .then((r) => setData(r.data)).catch((e) => setError(errMsg(e)));
  }, [q]);

  useEffect(() => { const t = setTimeout(cargar, 250); return () => clearTimeout(t); }, [cargar]);

  async function accion(u, tipo) {
    const textos = {
      desactivar: `¿Desactivar a ${u.username}? Se cerrarán sus sesiones.`,
      activar: `¿Activar a ${u.username}?`,
      bloquear: `¿Bloquear a ${u.username}? Solo un administrador podrá desbloquearlo.`,
      desbloquear: `¿Desbloquear a ${u.username}?`,
      reset: `¿Generar una contraseña temporal para ${u.username}? Se cerrarán sus sesiones.`,
    };
    if (!window.confirm(textos[tipo])) return;
    setError('');
    try {
      if (tipo === 'desactivar') await api.post(`/api/usuarios/${u.id}/estado`, { activo: false });
      if (tipo === 'activar') await api.post(`/api/usuarios/${u.id}/estado`, { activo: true });
      if (tipo === 'bloquear') await api.post(`/api/usuarios/${u.id}/bloquear`);
      if (tipo === 'desbloquear') await api.post(`/api/usuarios/${u.id}/desbloquear`);
      if (tipo === 'reset') {
        const r = await api.post(`/api/usuarios/${u.id}/reset-password`);
        setSecreto({ titulo: `Contraseña temporal de ${u.username}`, password: r.data.passwordTemporal });
      }
      cargar();
    } catch (err) { setError(errMsg(err)); }
  }

  const estaBloqueado = (u) => u.bloqueado && (!u.bloqueadoHasta || new Date(u.bloqueadoHasta + 'Z') > new Date());

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-xl font-semibold">Usuarios del sistema</h1>
        {gestionar && <button className="btn btn-primary" onClick={() => setNuevo(true)}>+ Nuevo usuario</button>}
      </div>
      <input className="input max-w-xs" placeholder="Buscar usuario o correo…" value={q.buscar} onChange={(e) => setQ({ buscar: e.target.value, page: 1 })} />
      <Alert>{error}</Alert>

      <div className="card overflow-x-auto">
        <table className="w-full">
          <thead><tr>
            <th className="th">Usuario</th><th className="th">Rol</th><th className="th">Estado</th><th className="th">Intentos</th>
            <th className="th">Último acceso</th><th className="th" />
          </tr></thead>
          <tbody>
            {data.items.map((u) => (
              <tr key={u.id} className="hover:bg-raised/60">
                <td className="td">{u.username}<div className="text-xs text-mute">{u.email}</div></td>
                <td className="td"><Badge tone="fox">{u.rol}</Badge></td>
                <td className="td space-x-1">
                  <Badge tone={u.activo ? 'ok' : 'bad'}>{u.activo ? 'Activo' : 'Inactivo'}</Badge>
                  {estaBloqueado(u) && <Badge tone="warn">Bloqueado</Badge>}
                  {u.debeCambiarPassword && <Badge>Pwd temporal</Badge>}
                </td>
                <td className="td font-mono">{u.intentosFallidos}</td>
                <td className="td text-xs">{fmtFechaHora(u.ultimoLogin)}<div className="text-mute font-mono">{u.ultimoLoginIp ?? ''}</div></td>
                <td className="td whitespace-nowrap text-right space-x-1">
                  {gestionar && u.id !== user.id && (<>
                    {u.bloqueado || u.intentosFallidos > 0
                      ? <button className="btn btn-sm" onClick={() => accion(u, 'desbloquear')}>Desbloquear</button>
                      : <button className="btn btn-sm" onClick={() => accion(u, 'bloquear')}>Bloquear</button>}
                    <button className="btn btn-sm" onClick={() => setEditar(u)}>Editar</button>
                    <button className="btn btn-sm" onClick={() => accion(u, 'reset')}>Reset pwd</button>
                    <button className={`btn btn-sm ${u.activo ? 'btn-danger' : ''}`} onClick={() => accion(u, u.activo ? 'desactivar' : 'activar')}>{u.activo ? 'Desactivar' : 'Activar'}</button>
                  </>)}
                </td>
              </tr>
            ))}
            {data.items.length === 0 && <tr><td className="td text-mute" colSpan={6}>Sin resultados.</td></tr>}
          </tbody>
        </table>
        <Pager page={q.page} pageSize={pageSize} total={data.total} onPage={(p) => setQ({ ...q, page: p })} />
      </div>

      {nuevo && <NuevoUsuario onClose={() => setNuevo(false)} onSaved={(pwd) => { setNuevo(false); setSecreto({ titulo: 'Usuario creado', password: pwd }); cargar(); }} />}
      {editar && <EditarUsuario u={editar} onClose={() => setEditar(null)} onSaved={() => { setEditar(null); cargar(); }} />}
      {secreto && (
        <Modal title={secreto.titulo} onClose={() => setSecreto(null)}>
          <p className="text-sm text-mute mb-3">Entregue esta contraseña de forma segura. No se volverá a mostrar y deberá cambiarse al primer ingreso.</p>
          <div className="flex gap-2">
            <input readOnly className="input font-mono" value={secreto.password} onFocus={(e) => e.target.select()} />
            <button className="btn" onClick={() => navigator.clipboard?.writeText(secreto.password)}>Copiar</button>
          </div>
        </Modal>
      )}
    </div>
  );
}
