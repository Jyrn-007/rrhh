# KITSUNE HRM — Sistema Integral de Recursos Humanos

React + Vite + Tailwind · ASP.NET Core 8 · EF Core (Pomelo) · MySQL 8

## Estado (Fase 1)
Incluido: login JWT con refresh rotativo, bloqueo por intentos fallidos, activo/inactivo, roles y permisos,
registro de accesos, auditoría antes/después, logs de errores, empleados (CRUD + baja lógica), usuarios,
dashboard, exportación a Excel.
Pendiente (Fases 2+): contratos, asistencia, vacaciones/permisos, nómina, capacitación, bajas y liquidaciones,
reportes PDF. Las tablas `contratos` y `ausencias` ya existen en el esquema.

## Puesta en marcha
1. **Base de datos**: ejecutar `database/01_schema.sql` y luego `database/02_seed.sql`.
2. **Backend** (`cd backend`), definir variables de entorno:
   ```
   ConnectionStrings__Default="Server=localhost;Port=3306;Database=kitsune_hrm;User=...;Password=...;"
   Jwt__Key="<cadena aleatoria de 32+ caracteres>"
   Seed__AdminUser=admin
   Seed__AdminEmail=admin@tuempresa.com
   Seed__AdminPassword="<contraseña temporal robusta>"
   Cors__Origins__0=http://localhost:5173
   ```
   Luego `dotnet run --urls http://localhost:5000`. Swagger en `/swagger` (solo Development).
   El admin inicial se crea solo si la tabla `usuarios` está vacía y debe cambiar su contraseña al entrar.
3. **Frontend** (`cd frontend`): `cp .env.example .env`, `npm install`, `npm run dev`.

## Seguridad implementada
- Contraseñas con BCrypt (cost 12) y política: 10+ caracteres, mayúscula, minúscula, número y símbolo.
- Bloqueo temporal tras 5 intentos fallidos (configurable en `Security:*`), desbloqueo automático o manual por admin.
- Rate limiting por IP en login/refresh (10 por minuto) además del bloqueo por cuenta.
- Access token de 15 min; refresh token aleatorio guardado como hash SHA-256, rotado en cada uso,
  con detección de reutilización (revoca todas las sesiones).
- En cada petición se valida que la cuenta siga activa y no bloqueada: desactivar/bloquear surte efecto de inmediato.
- Desactivar un empleado desactiva también su usuario y revoca sus sesiones.
- Permisos por rol (`[Authorize(Policy = "empleados.ver")]`, etc.), también ocultos en el frontend.
- Auditoría de toda operación de escritura (con valores antes/después) en la misma transacción que el cambio.
- Registro de accesos (login ok/fallido, bloqueo, logout, refresh, cambio de contraseña) con IP y user-agent.
- Excepciones no controladas se guardan en `logs_sistema`; el cliente solo recibe un mensaje genérico.
- Nunca se borran empleados ni usuarios físicamente (baja lógica).

## Notas
- El refresh token se guarda en `localStorage`. Para mayor endurecimiento se puede pasar a cookie HttpOnly
  (requiere mismo sitio o `SameSite=None; Secure` con CORS credentials).
- Los permisos viajan en el JWT: un cambio de rol se refleja al renovar el token (≤ 15 min).
- Supervisores aún ven todos los empleados; el filtro por equipo llega con la fase de jerarquías.

## Fase 2 — módulos por rol

Ejecute **database/04_fase2.sql** después de 01, 02 y 03 (idempotente; en Clever Cloud no incluye `USE`/`CREATE DATABASE`).

| Rol | Módulos |
|---|---|
| Empleado | Mi perfil, Mi asistencia (marcar entrada/salida), Mis solicitudes (vacaciones/permisos), Mis recibos, Mi desarrollo |
| Supervisor | Lo anterior + Mi equipo, aprobación de solicitudes, evaluaciones y objetivos de su equipo |
| RRHH | Empleados, organización, contratos, asistencia/horarios, ausencias, nómina, capacitación, bajas/liquidaciones, reportes |
| Administrador | Todo + usuarios, auditoría, accesos y logs |

Para ver las pantallas personales, cada usuario debe estar **vinculado a un empleado** (Usuarios → Editar) y volver a iniciar sesión. El alcance del supervisor se calcula con `empleados.jefe_id` (hasta 5 niveles).

### Limitaciones conocidas (validar con contabilidad)
- Nómina sin prorrateo para ingresos a mitad de mes; ISR se captura manualmente.
- IGSS 4.83 %, bonificación Q250 y factor de horas extra salen de la tabla `configuracion`.
- La liquidación es un estimado (indemnización solo en despido injustificado; aguinaldo y bono 14 proporcionales).
- No se consideran feriados en días hábiles.
# rrhh
