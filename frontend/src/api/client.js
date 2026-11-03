import axios from 'axios';

const BASE = import.meta.env.VITE_API_URL || 'http://localhost:5000';
const KEY_REFRESH = 'kh_refresh';
const KEY_USER = 'kh_user';

export const api = axios.create({ baseURL: BASE });

let accessToken = null;
let listener = () => {};
export const onSessionChange = (fn) => { listener = fn; };

const safe = {
  get: (k) => { try { return localStorage.getItem(k); } catch { return null; } },
  set: (k, v) => { try { localStorage.setItem(k, v); } catch { /* sin storage */ } },
  del: (k) => { try { localStorage.removeItem(k); } catch { /* sin storage */ } },
};

export function getStoredUser() {
  try { return JSON.parse(safe.get(KEY_USER)); } catch { return null; }
}
export const getStoredRefresh = () => safe.get(KEY_REFRESH);

export function applySession(data) {
  accessToken = data.accessToken;
  safe.set(KEY_REFRESH, data.refreshToken);
  safe.set(KEY_USER, JSON.stringify(data.usuario));
  listener(data.usuario);
}

export function clearSession() {
  accessToken = null;
  safe.del(KEY_REFRESH);
  safe.del(KEY_USER);
  listener(null);
}

api.interceptors.request.use((cfg) => {
  if (accessToken) cfg.headers.Authorization = `Bearer ${accessToken}`;
  return cfg;
});

let refreshing = null;
export function refreshSession() {
  const rt = getStoredRefresh();
  if (!rt) return Promise.reject(new Error('Sin sesión'));
  if (!refreshing) {
    refreshing = axios
      .post(`${BASE}/api/auth/refresh`, { refreshToken: rt })
      .then((r) => { applySession(r.data); return r.data; })
      .catch((e) => {
        // Solo se cierra la sesión si el servidor la rechaza; un fallo de red o un 429 no debe sacar al usuario.
        const st = e?.response?.status;
        if (st === 401 || st === 403) clearSession();
        throw e;
      })
      .finally(() => { refreshing = null; });
  }
  return refreshing;
}

api.interceptors.response.use(
  (r) => r,
  async (err) => {
    const { config, response } = err;
    if (response?.status === 401 && config && !config._retry && !config.url.includes('/api/auth/login')) {
      config._retry = true;
      try {
        const d = await refreshSession();
        config.headers.Authorization = `Bearer ${d.accessToken}`;
        return api(config);
      } catch { /* sesión perdida: clearSession ya notificó */ }
    }
    return Promise.reject(err);
  }
);

export function errMsg(e) {
  const d = e?.response?.data;
  if (d?.error) return d.error;
  if (d?.errors) return Object.values(d.errors).flat().join(' ');
  if (e?.response?.status === 429) return 'Demasiados intentos. Espere un momento.';
  if (e?.response?.status === 403) return 'No tiene permiso para esta acción.';
  if (!e?.response) return 'No se pudo conectar con el servidor.';
  return 'Ocurrió un error inesperado.';
}

/** Descarga un archivo (Excel) autenticado. */
export async function descargar(url, params, nombre) {
  const r = await api.get(url, { params, responseType: 'blob' });
  const href = URL.createObjectURL(r.data);
  const a = document.createElement('a');
  a.href = href;
  a.download = nombre;
  a.click();
  URL.revokeObjectURL(href);
}
