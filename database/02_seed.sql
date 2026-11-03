USE kitsune_hrm;

INSERT INTO roles (nombre, descripcion) VALUES
 ('Administrador', 'Acceso total al sistema'),
 ('RRHH',          'Gestión de personal, contratos, nóminas y reportes'),
 ('Supervisor',    'Gestión de su equipo, autorizaciones y asistencia'),
 ('Empleado',      'Autoservicio: perfil, solicitudes y recibos');

INSERT INTO permisos (codigo, modulo, descripcion) VALUES
 ('dashboard.ver',     'Dashboard',  'Ver dashboard'),
 ('empleados.ver',     'Empleados',  'Ver empleados'),
 ('empleados.crear',   'Empleados',  'Crear empleados'),
 ('empleados.editar',  'Empleados',  'Editar empleados'),
 ('empleados.estado',  'Empleados',  'Activar / desactivar empleados'),
 ('usuarios.ver',      'Seguridad',  'Ver usuarios'),
 ('usuarios.gestionar','Seguridad',  'Crear, editar, bloquear y desbloquear usuarios'),
 ('auditoria.ver',     'Seguridad',  'Ver auditoría, accesos y logs'),
 ('reportes.ver',      'Reportes',   'Ver y exportar reportes');

-- Administrador: todo
INSERT INTO roles_permisos SELECT 1, id FROM permisos;
-- RRHH
INSERT INTO roles_permisos SELECT 2, id FROM permisos
 WHERE codigo IN ('dashboard.ver','empleados.ver','empleados.crear','empleados.editar','empleados.estado','reportes.ver');
-- Supervisor
INSERT INTO roles_permisos SELECT 3, id FROM permisos WHERE codigo IN ('dashboard.ver','empleados.ver');
-- Empleado
INSERT INTO roles_permisos SELECT 4, id FROM permisos WHERE codigo IN ('dashboard.ver');

INSERT INTO configuracion (clave, valor, descripcion) VALUES
 ('empresa.nombre', 'KITSUNE HRM', 'Nombre de la empresa');

-- El usuario administrador inicial lo crea la API al primer arranque
-- (variables de entorno Seed__AdminUser / Seed__AdminPassword) para no guardar contraseñas en el repositorio.

-- Datos base de ejemplo (editar a gusto)
INSERT INTO sucursales (nombre) VALUES ('Casa Central');
INSERT INTO departamentos (nombre) VALUES ('Recursos Humanos'), ('Tecnología'), ('Administración'), ('Operaciones');
INSERT INTO puestos (nombre, departamento_id) VALUES
 ('Gerente de RRHH', 1), ('Analista de RRHH', 1), ('Desarrollador', 2), ('Soporte técnico', 2), ('Contador', 3), ('Operador', 4);
