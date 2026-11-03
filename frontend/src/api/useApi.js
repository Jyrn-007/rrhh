import { useCallback, useEffect, useState } from 'react';
import { api, errMsg } from './client.js';

/** Carga un GET y expone { data, error, loading, reload }. `enabled=false` pospone la carga. */
export function useApi(url, params, enabled = true) {
  const [data, setData] = useState(null);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(enabled);
  const key = JSON.stringify(params ?? {});

  const reload = useCallback(() => {
    if (!enabled) return Promise.resolve();
    setLoading(true);
    return api.get(url, { params })
      .then((r) => { setData(r.data); setError(''); })
      .catch((e) => setError(errMsg(e)))
      .finally(() => setLoading(false));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [url, key, enabled]);

  useEffect(() => { reload(); }, [reload]);
  return { data, error, loading, reload };
}

/** Ejecuta una acción mostrando el error en `setError`. Devuelve true si salió bien. */
export async function ejecutar(fn, setError) {
  setError('');
  try { await fn(); return true; } catch (e) { setError(errMsg(e)); return false; }
}
