import { useEffect } from 'react';

export function Field({ label, children, className = '' }) {
  return (
    <label className={className}>
      <span className="label">{label}</span>
      {children}
    </label>
  );
}

export function Badge({ tone = 'mute', children }) {
  const tones = {
    ok: 'text-ok border-ok/40 bg-ok/10',
    bad: 'text-bad border-bad/40 bg-bad/10',
    warn: 'text-warn border-warn/40 bg-warn/10',
    fox: 'text-fox border-fox/40 bg-fox/10',
    mute: 'text-mute border-line bg-raised',
  };
  return <span className={`inline-block text-xs px-2 py-0.5 rounded-full border ${tones[tone]}`}>{children}</span>;
}

export function Modal({ title, onClose, children, wide = false }) {
  useEffect(() => {
    const h = (e) => e.key === 'Escape' && onClose();
    window.addEventListener('keydown', h);
    return () => window.removeEventListener('keydown', h);
  }, [onClose]);
  return (
    <div className="fixed inset-0 z-50 flex items-start justify-center bg-black/70 p-4 overflow-y-auto" onMouseDown={onClose}>
      <div className={`card shadow-neon w-full ${wide ? 'max-w-3xl' : 'max-w-md'} my-8`} onMouseDown={(e) => e.stopPropagation()}>
        <div className="flex items-center justify-between px-5 py-3 border-b border-line">
          <h2 className="font-semibold">{title}</h2>
          <button className="text-mute hover:text-ink text-xl leading-none" onClick={onClose} aria-label="Cerrar">×</button>
        </div>
        <div className="p-5">{children}</div>
      </div>
    </div>
  );
}

export function Alert({ tone = 'bad', children }) {
  if (!children) return null;
  const c = tone === 'bad' ? 'border-bad/40 bg-bad/10 text-bad' : 'border-ok/40 bg-ok/10 text-ok';
  return <div className={`text-sm rounded-md border px-3 py-2 ${c}`}>{children}</div>;
}

export function Pager({ page, pageSize, total, onPage }) {
  const pages = Math.max(1, Math.ceil(total / pageSize));
  return (
    <div className="flex items-center justify-between px-3 py-2 text-xs text-mute">
      <span>{total} registro(s)</span>
      <div className="flex items-center gap-2">
        <button className="btn btn-sm" disabled={page <= 1} onClick={() => onPage(page - 1)}>Anterior</button>
        <span>Página {page} de {pages}</span>
        <button className="btn btn-sm" disabled={page >= pages} onClick={() => onPage(page + 1)}>Siguiente</button>
      </div>
    </div>
  );
}

export const fmtFecha = (d) => {
  if (!d) return '—';
  const m = /^(\d{4})-(\d{2})-(\d{2})/.exec(d); // fecha pura: evita corrimiento por zona horaria
  return m ? `${m[3]}/${m[2]}/${m[1]}` : new Date(d).toLocaleDateString('es-GT');
};
export const fmtFechaHora = (d) => (d ? new Date(d.endsWith?.('Z') ? d : d + 'Z').toLocaleString('es-GT') : '—');

export const fmtHora = (d) => (d ? String(d).substring(11, 16) : '—');
export const fmtQ = (n) => `Q ${Number(n ?? 0).toLocaleString('es-GT', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
export const MESES = ['', 'Enero', 'Febrero', 'Marzo', 'Abril', 'Mayo', 'Junio', 'Julio', 'Agosto', 'Septiembre', 'Octubre', 'Noviembre', 'Diciembre'];

export function Tabs({ tabs, value, onChange }) {
  return (
    <div className="flex flex-wrap gap-1 border-b border-line">
      {tabs.map((t) => (
        <button key={t.id} onClick={() => onChange(t.id)}
          className={`px-4 py-2 text-sm -mb-px border-b-2 ${value === t.id ? 'border-fox text-fox' : 'border-transparent text-mute hover:text-ink'}`}>
          {t.label}{t.badge ? <span className="ml-2 text-[10px] bg-fox text-canvas rounded-full px-1.5 py-0.5 font-semibold">{t.badge}</span> : null}
        </button>
      ))}
    </div>
  );
}

export function Vacio({ children = 'Sin registros.' }) {
  return <div className="td text-mute">{children}</div>;
}

export function SinEmpleado() {
  return (
    <div className="card p-6 max-w-xl">
      <h2 className="font-semibold mb-2">Usuario sin empleado vinculado</h2>
      <p className="text-sm text-mute">Esta sección es personal. Pida a un administrador que vincule su usuario a su ficha de empleado (Usuarios → Editar) y vuelva a iniciar sesión.</p>
    </div>
  );
}

const TONOS_ESTADO = {
  PRESENTE: 'ok', RETARDO: 'warn', FALTA: 'bad', PENDIENTE: 'warn', APROBADA: 'ok', RECHAZADA: 'bad', CANCELADA: 'mute',
  VIGENTE: 'ok', VENCIDO: 'bad', RENOVADO: 'mute', RESCINDIDO: 'bad', BORRADOR: 'warn', CERRADA: 'fox', PAGADA: 'ok',
  PROGRAMADA: 'fox', EN_CURSO: 'warn', FINALIZADA: 'ok', INSCRITO: 'mute', COMPLETADO: 'ok', NO_COMPLETADO: 'bad',
  EN_PROGRESO: 'warn', CUMPLIDO: 'ok', NO_CUMPLIDO: 'bad', ACTIVO: 'ok', INACTIVO: 'bad', RETIRADO: 'bad',
  VACACIONES: 'fox', PERMISO: 'warn', INCAPACIDAD: 'bad',
};
export function EstadoBadge({ estado }) {
  if (!estado) return <Badge>—</Badge>;
  return <Badge tone={TONOS_ESTADO[estado] ?? 'mute'}>{String(estado).replaceAll('_', ' ')}</Badge>;
}

export function Estrellas({ valor }) {
  return <span className="text-fox font-mono">{'★'.repeat(Math.round(valor))}<span className="text-line">{'★'.repeat(5 - Math.round(valor))}</span></span>;
}
