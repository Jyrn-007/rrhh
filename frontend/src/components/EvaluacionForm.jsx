import { useState } from 'react';
import { api } from '../api/client.js';
import { ejecutar } from '../api/useApi.js';
import { Alert, Field, Modal } from './ui.jsx';

const CRITERIOS = [['productividad', 'Productividad'], ['calidad', 'Calidad del trabajo'], ['trabajoEquipo', 'Trabajo en equipo'], ['puntualidad', 'Puntualidad']];

/** `empleados`: [{id, nombre}] que el usuario puede evaluar. */
export function EvaluacionForm({ empleados, onClose, onSaved }) {
  const anio = new Date().getFullYear();
  const [f, setF] = useState({ empleadoId: '', periodo: `${anio}-S${new Date().getMonth() < 6 ? 1 : 2}`, productividad: 3, calidad: 3, trabajoEquipo: 3, puntualidad: 3, comentarios: '' });
  const [err, setErr] = useState('');

  async function enviar(e) {
    e.preventDefault();
    const body = { ...f, empleadoId: Number(f.empleadoId), comentarios: f.comentarios || null };
    if (await ejecutar(() => api.post('/api/desarrollo/evaluaciones', body), setErr)) onSaved();
  }

  return (
    <Modal title="Nueva evaluación de desempeño" onClose={onClose}>
      <form onSubmit={enviar} className="space-y-3">
        <Alert>{err}</Alert>
        <Field label="Empleado *">
          <select className="input" required value={f.empleadoId} onChange={(e) => setF({ ...f, empleadoId: e.target.value })}>
            <option value="">—</option>{empleados.map((x) => <option key={x.id} value={x.id}>{x.nombre}</option>)}
          </select>
        </Field>
        <Field label="Periodo *"><input className="input" required maxLength={30} value={f.periodo} onChange={(e) => setF({ ...f, periodo: e.target.value })} /></Field>
        {CRITERIOS.map(([k, label]) => (
          <div key={k} className="flex items-center justify-between gap-3">
            <span className="text-sm">{label}</span>
            <div className="flex gap-1">
              {[1, 2, 3, 4, 5].map((n) => (
                <button type="button" key={n} onClick={() => setF({ ...f, [k]: n })}
                  className={`w-8 h-8 rounded border text-sm ${f[k] === n ? 'bg-fox border-fox text-canvas font-semibold' : 'border-line text-mute hover:border-mute'}`}>{n}</button>
              ))}
            </div>
          </div>
        ))}
        <Field label="Comentarios"><textarea className="input" rows={3} maxLength={1000} value={f.comentarios} onChange={(e) => setF({ ...f, comentarios: e.target.value })} /></Field>
        <div className="flex justify-end gap-2 pt-2"><button type="button" className="btn" onClick={onClose}>Cancelar</button><button className="btn btn-primary">Guardar</button></div>
      </form>
    </Modal>
  );
}

export function ObjetivoForm({ empleados, onClose, onSaved }) {
  const [f, setF] = useState({ empleadoId: '', titulo: '', descripcion: '', fechaLimite: '' });
  const [err, setErr] = useState('');

  async function enviar(e) {
    e.preventDefault();
    const body = { empleadoId: Number(f.empleadoId), titulo: f.titulo, descripcion: f.descripcion || null, fechaLimite: f.fechaLimite || null };
    if (await ejecutar(() => api.post('/api/desarrollo/objetivos', body), setErr)) onSaved();
  }

  return (
    <Modal title="Nuevo objetivo" onClose={onClose}>
      <form onSubmit={enviar} className="space-y-3">
        <Alert>{err}</Alert>
        <Field label="Empleado *">
          <select className="input" required value={f.empleadoId} onChange={(e) => setF({ ...f, empleadoId: e.target.value })}>
            <option value="">—</option>{empleados.map((x) => <option key={x.id} value={x.id}>{x.nombre}</option>)}
          </select>
        </Field>
        <Field label="Título *"><input className="input" required maxLength={150} value={f.titulo} onChange={(e) => setF({ ...f, titulo: e.target.value })} /></Field>
        <Field label="Descripción"><textarea className="input" rows={3} maxLength={500} value={f.descripcion} onChange={(e) => setF({ ...f, descripcion: e.target.value })} /></Field>
        <Field label="Fecha límite"><input type="date" className="input" value={f.fechaLimite} onChange={(e) => setF({ ...f, fechaLimite: e.target.value })} /></Field>
        <div className="flex justify-end gap-2 pt-2"><button type="button" className="btn" onClick={onClose}>Cancelar</button><button className="btn btn-primary">Crear</button></div>
      </form>
    </Modal>
  );
}
