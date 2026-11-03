import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext.jsx';
import { errMsg } from '../api/client.js';
import { Alert } from '../components/ui.jsx';

export default function Login() {
  const { login } = useAuth();
  const nav = useNavigate();
  const [usuario, setUsuario] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);

  async function submit(e) {
    e.preventDefault();
    setBusy(true);
    setError('');
    try {
      const u = await login(usuario, password);
      nav(u.debeCambiarPassword ? '/cambiar-password' : '/', { replace: true });
    } catch (err) {
      setError(errMsg(err));
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="min-h-full flex items-center justify-center p-4"
         style={{ backgroundImage: 'radial-gradient(circle at 20% 10%, rgba(255,122,26,.12), transparent 40%), radial-gradient(circle at 80% 90%, rgba(124,58,237,.10), transparent 40%)' }}>
      <form onSubmit={submit} className="card shadow-neon w-full max-w-sm p-7 space-y-4">
        <div className="text-center mb-2">
          <img src="/fox.svg" alt="" className="w-14 h-14 mx-auto mb-3" />
          <h1 className="text-2xl font-bold tracking-[0.25em] text-fox">KITSUNE</h1>
          <p className="text-xs tracking-[0.35em] text-mute mt-1">HUMAN RESOURCES MANAGEMENT</p>
        </div>
        <Alert>{error}</Alert>
        <div>
          <label className="label" htmlFor="u">Usuario o correo</label>
          <input id="u" className="input" value={usuario} onChange={(e) => setUsuario(e.target.value)} autoComplete="username" autoFocus required />
        </div>
        <div>
          <label className="label" htmlFor="p">Contraseña</label>
          <input id="p" type="password" className="input" value={password} onChange={(e) => setPassword(e.target.value)} autoComplete="current-password" required />
        </div>
        <button className="btn btn-primary w-full" disabled={busy}>{busy ? 'Ingresando…' : 'Ingresar'}</button>
        <p className="text-[11px] text-mute text-center">Los accesos y los intentos fallidos quedan registrados.</p>
      </form>
    </div>
  );
}
