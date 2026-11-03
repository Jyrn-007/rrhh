USE kitsune_hrm;

-- Usuarios iniciales (uno por rol). Contraseñas temporales: deben cambiarse al primer ingreso.
-- Los correos @kitsune.local son de ejemplo: cámbielos por los reales.
-- Si ya existe un usuario con el mismo username, no se vuelve a insertar.
INSERT INTO usuarios (username, email, password_hash, rol_id, activo, debe_cambiar_password)
SELECT v.username, v.email, v.password_hash, v.rol_id, v.activo, v.debe
FROM (
  SELECT 'admin' AS username, 'admin@kitsune.local' AS email, '$2b$12$Zppk6pHcqIPNGkmr.2GKI.VD.P2aBXtmlRSeENfwfNjrMp5WjrUN6' AS password_hash, (SELECT id FROM roles WHERE nombre = 'Administrador') AS rol_id, 1 AS activo, 1 AS debe
 UNION ALL
  SELECT 'rrhh' AS username, 'rrhh@kitsune.local' AS email, '$2b$12$RYCw0XUBeDN7FJxL.UDDJuBB7Zzj9XyqBIMTatIQVhbMVEjL4.ZaS' AS password_hash, (SELECT id FROM roles WHERE nombre = 'RRHH') AS rol_id, 1 AS activo, 1 AS debe
 UNION ALL
  SELECT 'supervisor' AS username, 'supervisor@kitsune.local' AS email, '$2b$12$fFRqUtCUlk55x9YYYbzvVORimoWv4Pamzm8cwf6lUujzpZSSB3hwa' AS password_hash, (SELECT id FROM roles WHERE nombre = 'Supervisor') AS rol_id, 1 AS activo, 1 AS debe
 UNION ALL
  SELECT 'empleado' AS username, 'empleado@kitsune.local' AS email, '$2b$12$kN/au5Q8Q9n9s.JzF3/32uiFY5mWVWuGLfPm3hkVvz1MGirws6Qju' AS password_hash, (SELECT id FROM roles WHERE nombre = 'Empleado') AS rol_id, 1 AS activo, 1 AS debe
) v
WHERE NOT EXISTS (SELECT 1 FROM usuarios x WHERE x.username = v.username);

SELECT u.id, u.username, u.email, r.nombre AS rol, u.activo, u.debe_cambiar_password
FROM usuarios u JOIN roles r ON r.id = u.rol_id ORDER BY u.id;
