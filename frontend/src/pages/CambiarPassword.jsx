import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext.jsx';
import { errMsg } from '../api/client.js';
import { Alert } from '../components/ui.jsx';

export default function CambiarPassword() {
  const { user, cambiarPassword } = useAuth();
  const nav = useNavigate();
  const [actual, setActual] = useState('');
  const [nueva, setNueva] = useState('');
  const [repetir, setRepetir] = useState('');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);

  async function submit(e) {
    e.preventDefault();
    setError('');
    if (nueva !== repetir) return setError('Las contraseñas no coinciden.');
    setBusy(true);
    try {
      await cambiarPassword(actual, nueva);
      nav('/', { replace: true });
    } catch (err) {
      setError(errMsg(err));
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="min-h-full flex items-center justify-center p-4">
      <form onSubmit={submit} className="card shadow-neon w-full max-w-sm p-7 space-y-4">
        <h1 className="text-lg font-semibold text-fox">Cambiar contraseña</h1>
        {user?.debeCambiarPassword && <Alert tone="ok">Debe establecer una contraseña nueva antes de continuar.</Alert>}
        <Alert>{error}</Alert>
        <div><label className="label">Contraseña actual</label><input type="password" className="input" value={actual} onChange={(e) => setActual(e.target.value)} autoComplete="current-password" required /></div>
        <div><label className="label">Contraseña nueva</label><input type="password" className="input" value={nueva} onChange={(e) => setNueva(e.target.value)} autoComplete="new-password" required /></div>
        <div><label className="label">Repetir contraseña nueva</label><input type="password" className="input" value={repetir} onChange={(e) => setRepetir(e.target.value)} autoComplete="new-password" required /></div>
        <p className="text-[11px] text-mute">Mínimo 10 caracteres, con mayúscula, minúscula, número y símbolo.</p>
        <div className="flex gap-2">
          {!user?.debeCambiarPassword && <button type="button" className="btn flex-1" onClick={() => nav(-1)}>Cancelar</button>}
          <button className="btn btn-primary flex-1" disabled={busy}>{busy ? 'Guardando…' : 'Guardar'}</button>
        </div>
      </form>
    </div>
  );
}
