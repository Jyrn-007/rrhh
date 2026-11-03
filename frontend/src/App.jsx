import { Navigate, Route, Routes } from 'react-router-dom';
import { useAuth } from './auth/AuthContext.jsx';
import Layout from './components/Layout.jsx';
import Login from './pages/Login.jsx';
import CambiarPassword from './pages/CambiarPassword.jsx';
import Dashboard from './pages/Dashboard.jsx';
import Empleados from './pages/Empleados.jsx';
import Usuarios from './pages/Usuarios.jsx';
import Auditoria from './pages/Auditoria.jsx';
import MiPerfil from './pages/MiPerfil.jsx';
import MiAsistencia from './pages/MiAsistencia.jsx';
import MisSolicitudes from './pages/MisSolicitudes.jsx';
import MisRecibos from './pages/MisRecibos.jsx';
import MiDesarrollo from './pages/MiDesarrollo.jsx';
import MiEquipo from './pages/MiEquipo.jsx';
import Organizacion from './pages/Organizacion.jsx';
import Contratos from './pages/Contratos.jsx';
import Asistencia from './pages/Asistencia.jsx';
import Ausencias from './pages/Ausencias.jsx';
import Nomina from './pages/Nomina.jsx';
import Capacitacion from './pages/Capacitacion.jsx';
import Bajas from './pages/Bajas.jsx';
import Reportes from './pages/Reportes.jsx';

function Protegida({ perm, children }) {
  const { user, loading, can } = useAuth();
  if (loading) return <div className="p-10 text-mute">Cargando…</div>;
  if (!user) return <Navigate to="/login" replace />;
  if (user.debeCambiarPassword) return <Navigate to="/cambiar-password" replace />;
  if (perm && !can(perm)) return <div className="card p-6 text-mute">No tiene permiso para ver esta sección.</div>;
  return children;
}

const rutas = [
  ['/', 'dashboard.ver', Dashboard],
  ['mi/perfil', 'perfil.ver', MiPerfil],
  ['mi/asistencia', 'asistencia.marcar', MiAsistencia],
  ['mi/solicitudes', 'ausencias.solicitar', MisSolicitudes],
  ['mi/recibos', 'nomina.propia', MisRecibos],
  ['mi/desarrollo', 'desarrollo.propio', MiDesarrollo],
  ['equipo', 'equipo.ver', MiEquipo],
  ['empleados', 'empleados.ver', Empleados],
  ['organizacion', 'organizacion.ver', Organizacion],
  ['contratos', 'contratos.ver', Contratos],
  ['asistencia', 'asistencia.ver', Asistencia],
  ['ausencias', 'ausencias.ver', Ausencias],
  ['nomina', 'nomina.ver', Nomina],
  ['capacitacion', 'capacitacion.ver', Capacitacion],
  ['bajas', 'bajas.ver', Bajas],
  ['reportes', 'reportes.ver', Reportes],
  ['usuarios', 'usuarios.ver', Usuarios],
  ['auditoria', 'auditoria.ver', Auditoria],
];

export default function App() {
  const { user, loading } = useAuth();
  return (
    <Routes>
      <Route path="/login" element={user && !loading ? <Navigate to="/" replace /> : <Login />} />
      <Route path="/cambiar-password" element={loading ? null : user ? <CambiarPassword /> : <Navigate to="/login" replace />} />
      <Route element={<Protegida><Layout /></Protegida>}>
        {rutas.map(([path, perm, Page]) => (
          path === '/'
            ? <Route key={path} index element={<Protegida perm={perm}><Page /></Protegida>} />
            : <Route key={path} path={path} element={<Protegida perm={perm}><Page /></Protegida>} />
        ))}
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}
