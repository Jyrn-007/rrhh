import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import {
  api, applySession, clearSession, getStoredRefresh, getStoredUser, onSessionChange, refreshSession,
} from '../api/client.js';

const Ctx = createContext(null);
export const useAuth = () => useContext(Ctx);

export function AuthProvider({ children }) {
  const [user, setUser] = useState(getStoredUser());
  const [loading, setLoading] = useState(!!getStoredRefresh());

  useEffect(() => {
    onSessionChange(setUser);
    if (getStoredRefresh()) {
      refreshSession().catch(() => {}).finally(() => setLoading(false));
    }
  }, []);

  const login = useCallback(async (usuario, password) => {
    const { data } = await api.post('/api/auth/login', { usuario, password });
    applySession(data);
    return data.usuario;
  }, []);

  const logout = useCallback(async () => {
    try { await api.post('/api/auth/logout', { refreshToken: getStoredRefresh() || '' }); } catch { /* ya expirada */ }
    clearSession();
  }, []);

  const cambiarPassword = useCallback(async (passwordActual, passwordNueva) => {
    const { data } = await api.post('/api/auth/cambiar-password', { passwordActual, passwordNueva });
    applySession(data);
  }, []);

  const can = useCallback((p) => !!user?.permisos?.includes(p), [user]);

  const value = useMemo(() => ({ user, loading, login, logout, cambiarPassword, can }), [user, loading, login, logout, cambiarPassword, can]);
  return <Ctx.Provider value={value}>{children}</Ctx.Provider>;
}
