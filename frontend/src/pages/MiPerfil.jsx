import { useEffect, useState } from 'react';
import { api } from '../api/client.js';
import { ejecutar, useApi } from '../api/useApi.js';
import { Alert, Badge, Field, SinEmpleado, fmtFecha, fmtQ } from '../components/ui.jsx';

const Dato = ({ k, v }) => (<div><div className="text-xs text-mute">{k}</div><div className="text-sm">{v || '—'}</div></div>);

export default function MiPerfil() {
  const { data, error, reload } = useApi('/api/mi/perfil');
  const [f, setF] = useState(null);
  const [msg, setMsg] = useState('');
  const [err, setErr] = useState('');

  useEffect(() => {
    if (data) {
      const e = data.empleado;
      setF({
        email: e.email ?? '', telefono: e.telefono ?? '', direccion: e.direccion ?? '',
        contactoEmergenciaNombre: e.contactoEmergenciaNombre ?? '', contactoEmergenciaTelefono: e.contactoEmergenciaTelefono ?? '',
        contactoEmergenciaParentesco: e.contactoEmergenciaParentesco ?? '',
      });
    }
  }, [data]);

  if (error?.includes('vinculado')) return <SinEmpleado />;
  if (error) return <Alert>{error}</Alert>;
  if (!data || !f) return <div className="text-mute">Cargando…</div>;
  const { empleado: e, contrato, vacaciones, usuario } = data;
  const set = (k) => (ev) => setF((p) => ({ ...p, [k]: ev.target.value }));

  async function guardar(ev) {
    ev.preventDefault();
    setMsg('');
    const body = Object.fromEntries(Object.entries(f).map(([k, v]) => [k, v === '' ? null : v]));
    if (await ejecutar(() => api.put('/api/mi/perfil', body), setErr)) { setMsg('Datos actualizados.'); reload(); }
  }

  return (
    <div className="space-y-6 max-w-4xl">
      <h1 className="text-xl font-semibold">Mi perfil</h1>

      <section className="card p-5">
        <div className="flex items-center justify-between mb-4">
          <div><div className="text-lg font-semibold">{e.nombres} {e.apellidos}</div><div className="text-sm text-mute">{e.puesto} · {e.departamento}</div></div>
          <Badge tone="fox">{e.codigo}</Badge>
        </div>
        <div className="grid sm:grid-cols-3 gap-4">
          <Dato k="DPI" v={e.dpi} /><Dato k="NIT" v={e.nit} /><Dato k="Fecha de nacimiento" v={fmtFecha(e.fechaNacimiento)} />
          <Dato k="Ingreso" v={fmtFecha(e.fechaIngreso)} /><Dato k="Sucursal" v={e.sucursal} /><Dato k="Jefe inmediato" v={e.jefe} />
          <Dato k="Usuario" v={usuario.username} /><Dato k="Rol" v={usuario.rol} />
        </div>
      </section>

      <div className="grid md:grid-cols-2 gap-6">
        <section className="card p-5">
          <h2 className="font-medium mb-3">Contrato vigente</h2>
          {contrato ? (
            <div className="grid grid-cols-2 gap-3">
              <Dato k="Tipo" v={contrato.tipo.replaceAll('_', ' ')} /><Dato k="Salario" v={fmtQ(contrato.salario)} />
              <Dato k="Inicio" v={fmtFecha(contrato.fechaInicio)} /><Dato k="Fin" v={contrato.fechaFin ? fmtFecha(contrato.fechaFin) : 'Indefinido'} />
            </div>
          ) : <p className="text-sm text-mute">Sin contrato vigente registrado.</p>}
        </section>
        <section className="card p-5">
          <h2 className="font-medium mb-3">Vacaciones</h2>
          <div className="grid grid-cols-4 gap-3 text-center">
            <Dato k="Años" v={vacaciones.anios} /><Dato k="Derecho" v={vacaciones.derecho} /><Dato k="Usados" v={vacaciones.usados} />
            <div><div className="text-xs text-mute">Disponibles</div><div className="text-lg font-bold text-fox">{vacaciones.disponible}</div></div>
          </div>
        </section>
      </div>

      <form onSubmit={guardar} className="card p-5 space-y-4">
        <h2 className="font-medium">Datos de contacto</h2>
        <Alert>{err}</Alert>
        <Alert tone="ok">{msg}</Alert>
        <div className="grid sm:grid-cols-2 gap-3">
          <Field label="Correo"><input type="email" className="input" value={f.email} onChange={set('email')} maxLength={150} /></Field>
          <Field label="Teléfono"><input className="input" value={f.telefono} onChange={set('telefono')} maxLength={30} /></Field>
          <Field label="Dirección" className="sm:col-span-2"><input className="input" value={f.direccion} onChange={set('direccion')} maxLength={250} /></Field>
        </div>
        <h3 className="text-xs uppercase tracking-wide text-fox">Contacto de emergencia</h3>
        <div className="grid sm:grid-cols-3 gap-3">
          <Field label="Nombre"><input className="input" value={f.contactoEmergenciaNombre} onChange={set('contactoEmergenciaNombre')} maxLength={120} /></Field>
          <Field label="Teléfono"><input className="input" value={f.contactoEmergenciaTelefono} onChange={set('contactoEmergenciaTelefono')} maxLength={30} /></Field>
          <Field label="Parentesco"><input className="input" value={f.contactoEmergenciaParentesco} onChange={set('contactoEmergenciaParentesco')} maxLength={50} /></Field>
        </div>
        <div className="flex justify-end"><button className="btn btn-primary">Guardar cambios</button></div>
        <p className="text-[11px] text-mute">Para cambiar DPI, NIT, puesto u otros datos laborales, contacte a RRHH.</p>
      </form>
    </div>
  );
}
