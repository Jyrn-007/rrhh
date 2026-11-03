import { useState } from 'react';
import { errMsg } from '../api/client.js';
import { Alert, Field, Modal } from './ui.jsx';

/**
 * Formulario genérico en modal.
 * fields: [{ k, label, type?: text|number|date|time|email|textarea|select|checkbox, options?: [{value,label}], required?, span?: 1|2, hint?, visible?: (f)=>bool, step?, maxLength? }]
 * onSubmit(valores) debe lanzar si falla; si termina bien el llamador cierra el modal.
 */
export default function FormModal({ title, fields, initial = {}, onClose, onSubmit, submitLabel = 'Guardar', wide = false, children }) {
  const [f, setF] = useState(() => {
    const base = {};
    fields.forEach((x) => { base[x.k] = x.type === 'checkbox' ? true : ''; });
    Object.entries(initial).forEach(([k, v]) => { if (v !== null && v !== undefined) base[k] = v; });
    return base;
  });
  const [err, setErr] = useState('');
  const [busy, setBusy] = useState(false);

  async function submit(e) {
    e.preventDefault();
    setBusy(true);
    setErr('');
    try { await onSubmit(f); } catch (x) { setErr(errMsg(x)); } finally { setBusy(false); }
  }

  return (
    <Modal title={title} onClose={onClose} wide={wide}>
      <form onSubmit={submit} className="space-y-3">
        <Alert>{err}</Alert>
        <div className="grid sm:grid-cols-2 gap-3">
          {fields.filter((x) => !x.visible || x.visible(f)).map((x) => {
            const span = x.span === 2 || x.type === 'textarea' ? 'sm:col-span-2' : '';
            const set = (v) => setF((p) => ({ ...p, [x.k]: v }));
            if (x.type === 'checkbox') {
              return (
                <label key={x.k} className={`flex items-center gap-2 text-sm ${span}`}>
                  <input type="checkbox" checked={!!f[x.k]} onChange={(e) => set(e.target.checked)} className="accent-orange-500" /> {x.label}
                </label>
              );
            }
            return (
              <Field key={x.k} label={x.label + (x.required ? ' *' : '')} className={span}>
                {x.type === 'select' ? (
                  <select className="input" disabled={x.disabled} required={x.required} value={f[x.k]} onChange={(e) => set(e.target.value)}>
                    <option value="">—</option>
                    {x.options.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}
                  </select>
                ) : x.type === 'textarea' ? (
                  <textarea className="input" rows={3} required={x.required} maxLength={x.maxLength} value={f[x.k]} onChange={(e) => set(e.target.value)} />
                ) : (
                  <input className="input" disabled={x.disabled} type={x.type ?? 'text'} step={x.step} required={x.required} maxLength={x.maxLength} value={f[x.k]} onChange={(e) => set(e.target.value)} />
                )}
                {x.hint && <span className="text-[11px] text-mute">{x.hint}</span>}
              </Field>
            );
          })}
        </div>
        {children}
        <div className="flex justify-end gap-2 pt-2">
          <button type="button" className="btn" onClick={onClose}>Cancelar</button>
          <button className="btn btn-primary" disabled={busy}>{busy ? 'Guardando…' : submitLabel}</button>
        </div>
      </form>
    </Modal>
  );
}

/** Convierte '' en null y los campos indicados en número. */
export function limpiar(f, numericos = []) {
  const o = {};
  Object.entries(f).forEach(([k, v]) => {
    if (v === '' || v === undefined) o[k] = null;
    else o[k] = numericos.includes(k) ? Number(v) : v;
  });
  return o;
}

export const opciones = (lista, v = 'id', l = 'nombre') => (lista ?? []).map((x) => ({ value: x[v], label: x[l] }));
