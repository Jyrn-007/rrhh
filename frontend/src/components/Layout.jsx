import { NavLink, Outlet, useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext.jsx';

// personal=true: solo se muestra si el usuario está vinculado a un empleado
const secciones = [
  {
    titulo: 'Mi espacio',
    items: [
      { to: '/', label: 'Dashboard', perm: 'dashboard.ver', icon: '◈', end: true },
      { to: '/mi/perfil', label: 'Mi perfil', perm: 'perfil.ver', icon: '☺', personal: true },
      { to: '/mi/asistencia', label: 'Mi asistencia', perm: 'asistencia.marcar', icon: '◷', personal: true },
      { to: '/mi/solicitudes', label: 'Mis solicitudes', perm: 'ausencias.solicitar', icon: '✈', personal: true },
      { to: '/mi/recibos', label: 'Mis recibos', perm: 'nomina.propia', icon: '▤', personal: true },
      { to: '/mi/desarrollo', label: 'Mi desarrollo', perm: 'desarrollo.propio', icon: '↗', personal: true },
    ],
  },
  { titulo: 'Mi equipo', items: [{ to: '/equipo', label: 'Mi equipo', perm: 'equipo.ver', icon: '⚑' }] },
  {
    titulo: 'Gestión de RRHH',
    items: [
      { to: '/empleados', label: 'Empleados', perm: 'empleados.ver', icon: '☷' },
      { to: '/organizacion', label: 'Organización', perm: 'organizacion.ver', icon: '⌂' },
      { to: '/contratos', label: 'Contratos', perm: 'contratos.ver', icon: '✎' },
      { to: '/asistencia', label: 'Asistencia', perm: 'asistencia.ver', icon: '◴' },
      { to: '/ausencias', label: 'Vacaciones y permisos', perm: 'ausencias.ver', icon: '☀' },
      { to: '/nomina', label: 'Nómina', perm: 'nomina.ver', icon: '¤' },
      { to: '/capacitacion', label: 'Capacitación y evaluaciones', perm: 'capacitacion.ver', icon: '✧' },
      { to: '/bajas', label: 'Bajas y liquidaciones', perm: 'bajas.ver', icon: '⇥' },
      { to: '/reportes', label: 'Reportes', perm: 'reportes.ver', icon: '▥' },
    ],
  },
  {
    titulo: 'Administración',
    items: [
      { to: '/usuarios', label: 'Usuarios', perm: 'usuarios.ver', icon: '⚿' },
      { to: '/auditoria', label: 'Auditoría y logs', perm: 'auditoria.ver', icon: '⌕' },
    ],
  },
];

export default function Layout() {
  const { user, logout, can } = useAuth();
  const nav = useNavigate();
  const visible = (i) => can(i.perm) && (!i.personal || user?.empleadoId);

  return (
    <div className="min-h-full flex">
      <aside className="w-64 shrink-0 bg-panel border-r border-line flex flex-col max-h-screen sticky top-0">
        <div className="px-5 py-5 border-b border-line">
          <div className="flex items-center gap-2">
            <img src="/fox.svg" alt="" className="w-8 h-8" />
            <div>
              <div className="font-bold tracking-widest text-fox leading-none">KITSUNE</div>
              <div className="text-[10px] tracking-[0.3em] text-mute mt-1">HRM</div>
            </div>
          </div>
        </div>
        <nav className="flex-1 p-3 space-y-4 overflow-y-auto">
          {secciones.map((s) => {
            const items = s.items.filter(visible);
            if (items.length === 0) return null;
            return (
              <div key={s.titulo}>
                <div className="px-3 mb-1 text-[10px] uppercase tracking-[0.2em] text-mute/70">{s.titulo}</div>
                <div className="space-y-1">
                  {items.map((i) => (
                    <NavLink key={i.to} to={i.to} end={i.end}
                      className={({ isActive }) =>
                        `flex items-center gap-3 px-3 py-2 rounded-md text-sm transition ${isActive ? 'bg-fox/10 text-fox shadow-neon' : 'text-mute hover:text-ink hover:bg-raised'}`}>
                      <span className="w-4 text-center">{i.icon}</span>
                      {i.label}
                    </NavLink>
                  ))}
                </div>
              </div>
            );
          })}
        </nav>
        <div className="p-4 border-t border-line text-xs">
          <div className="font-medium text-ink truncate">{user?.username}</div>
          <div className="text-mute mb-3">{user?.rol}</div>
          <div className="flex gap-2">
            <button className="btn btn-sm flex-1" onClick={() => nav('/cambiar-password')}>Contraseña</button>
            <button className="btn btn-sm flex-1" onClick={async () => { await logout(); nav('/login'); }}>Salir</button>
          </div>
        </div>
      </aside>
      <main className="flex-1 min-w-0 p-6 lg:p-8 overflow-x-auto">
        <Outlet />
      </main>
    </div>
  );
}
